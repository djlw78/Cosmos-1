using Cosmos.Buffer;
using Cosmos.Codec;
using Cosmos.Token;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Thrift.Protocol;

namespace Cosmos.Client
{
    public class Bootstrap
    {

        #region Immutable construction member variables

        readonly Setting _setting;
        readonly IMessageSerializer _messageSerializer;

        readonly BufferManager _readBufferManager;
        readonly BufferManager _writeBufferManager;

        #endregion

        #region SAEA
        SocketAsyncEventArgs _saeaWrite;
        #endregion

        #region Event handler
        public delegate void ConnectEventHandler(object sender);
        public delegate void ReadEventHandler(object sender, int handlerId, byte[] payload);
        public delegate void DisconnectEventHandler(object sender);
        public delegate void SocketErrorEventHandler(object sender, SocketError socketError);

        public event ConnectEventHandler OnConnected;
        public event ReadEventHandler OnRead;
        public event DisconnectEventHandler OnDisconnected;
        public event SocketErrorEventHandler OnSocketError;
        #endregion

        private bool _isConnected = false;

        public bool IsConnected
        {
            get
            {
                return _isConnected;
            }
        }

        public Bootstrap(Setting setting, IMessageSerializer messageSerializer)
        {
            this._setting = setting;
            this._messageSerializer = messageSerializer;

            this._readBufferManager = new BufferManager(setting.ReadBufferSize, 1, _messageSerializer.GetHeaderSize());
            this._writeBufferManager = new BufferManager(setting.WriteBufferSize, 1, 0);

            Init();
        }

        private void Init()
        {
            _readBufferManager.InitBuffer();
            _writeBufferManager.InitBuffer();
        }

        public void Connect(string host, int port)
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(host), port);
            Connect(remoteEP);
        }

        public void Disconnect()
        {
            if (_isConnected == false) { return; }
            _isConnected = false;
            _saeaWrite.AcceptSocket.Shutdown(SocketShutdown.Both);
            _saeaWrite.AcceptSocket.Close();
        }

        public void Connect(IPEndPoint remoteEP)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            SocketAsyncEventArgs saeaConnect = new SocketAsyncEventArgs();

            saeaConnect.RemoteEndPoint = remoteEP;
            saeaConnect.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
            saeaConnect.UserToken = socket;

            bool willRaiseEvent = socket.ConnectAsync(saeaConnect);

            if (!willRaiseEvent)
            {
                ProcessConnect(saeaConnect);
            }
        }

        private void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    ProcessConnect(e);
                    break;
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
            }
        }

        /// <summary>
        /// Socket 끊어졌을 때 처리
        /// </summary>
        /// <param name="e"></param>
        private void CloseSocket(SocketAsyncEventArgs e)
        {
            if (e == null || e.AcceptSocket == null) return;
            Socket socket = e.AcceptSocket;
            _isConnected = false;
            OnDisconnected(this);

            if (socket != null && socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
        }

        private void ProcessConnect(SocketAsyncEventArgs saeaConnect)
        {
            if (saeaConnect.SocketError == SocketError.Success)
            {
                Trace.Write("Creating 1 SocketEventAsyncArgs for read...", "[INFO]");
                SocketAsyncEventArgs _readSaea = new SocketAsyncEventArgs();
                _readSaea.AcceptSocket = (Socket)saeaConnect.UserToken;                
                _readBufferManager.SetBuffer(_readSaea);
                _readSaea.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                _readSaea.UserToken = new ReadToken(_readSaea, _setting.ReadBufferSize, _messageSerializer.GetHeaderSize());
                Trace.WriteLine("Done!");

                Trace.Write("Creating 1 SocketEventAsyncArgs for write...", "[INFO]");
                _saeaWrite = new SocketAsyncEventArgs();
                _writeBufferManager.SetBuffer(_saeaWrite);
                _saeaWrite.AcceptSocket = (Socket)saeaConnect.UserToken;
                _saeaWrite.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                _saeaWrite.UserToken = new WriteToken(_saeaWrite, _setting.WriteBufferSize);

                Trace.WriteLine("Done!");
                _isConnected = true;
                OnConnected(this);
                StartReceiveHeader(_readSaea);
            }
        }

        private void StartReceiveHeader(SocketAsyncEventArgs e)
        {
            Debug.WriteLine("StartReceiveHeader", "[DEBUG]");
            ReadToken rt = (ReadToken)e.UserToken;
            rt.Initialize();

            e.SetBuffer(rt.HeaderOffset, _messageSerializer.GetHeaderSize());   // 최초에는 MessageHeaderSize 만큼만 읽는다.            
            bool willRaiseEvent = rt.Socket.ReceiveAsync(e);
            if (!willRaiseEvent)
            {
                ProcessReceive(e);
            }
        }

        /// <summary>
        /// 헤더를 다 못받았을 경우
        /// </summary>
        private void ContinueReceiveHeader(SocketAsyncEventArgs e, int bytesRead, int nextBytesToRead)
        {
            ReadToken rt = (ReadToken)e.UserToken;
            e.SetBuffer(rt.HeaderOffset + bytesRead, nextBytesToRead);

            bool willRaiseEvent = rt.Socket.ReceiveAsync(e);
            if (!willRaiseEvent)
            {
                ProcessReceive(e);
            }
        }

        /// <summary>
        /// 더 읽어올 Message 가 있는 경우
        /// </summary>
        /// <param name="e"></param>
        /// <param name="nextBufferSizeToRead">Socket buffer에서 다음으로 읽어올 buffer size</param>
        private void ContinueReceive(SocketAsyncEventArgs e, int nextBufferSizeToRead)
        {
            Debug.WriteLine("ContinueReceive", "[DEBUG]");
            ReadToken rt = (ReadToken)e.UserToken;
            e.SetBuffer(e.Offset, nextBufferSizeToRead);

            bool willRaiseEvent = rt.Socket.ReceiveAsync(e);
            if (!willRaiseEvent)
            {
                ProcessReceive(e);
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            ReadToken rt = (ReadToken)e.UserToken;

            if (e.SocketError != SocketError.Success)
            {
                HandleError(e);
                return;
            }

            if (e.BytesTransferred == 0)
            {
                CloseSocket(e);
                return;
            }

            int nextBufferSizeToRead = 0;

            if (IsStartOfMessage(rt, e.BytesTransferred)) // 메세지의 시작 부분일 경우
            {
                int totalHeaderReceived = rt.IncrementHeaderLength();

                // 헤더의 길이가 충분하지 않은 경우
                if (totalHeaderReceived < _messageSerializer.GetHeaderSize())
                {
                    nextBufferSizeToRead = _messageSerializer.GetHeaderSize() - totalHeaderReceived;
                    ContinueReceiveHeader(e, e.BytesTransferred, nextBufferSizeToRead);
                    return;
                }

                int totalDataLength, handlerId;

                // 헤더 정보가 올바른지 검증
                if (_messageSerializer.CheckHaderValidation(e.Buffer, rt.HeaderOffset, out totalDataLength, out handlerId))
                {
                    rt.TotalDataLength = totalDataLength;
                    rt.HandlerId = handlerId;
                    rt.AssignTotalData(totalDataLength);

                    nextBufferSizeToRead = rt.NextBufferSizeToReceive;
                }
                else
                {
                    StartReceiveHeader(e); // 헤더가 올바르지 않으면 다시 Receive 시작
                    return;
                }
            }
            else
            {
                // 메세지가 받는 중일 경우 session 객체 내부의 byte array에 socket buffer에서 읽어온 내용을 추가해준다.
                nextBufferSizeToRead = rt.AddTotalData();
            }

            if (nextBufferSizeToRead > 0) //다음으로 읽어올 데이터가 더 있는 경우
            {
                ContinueReceive(e, nextBufferSizeToRead);
            }
            else
            {                            
                OnRead(this, rt.HandlerId, rt.TotalData);
                StartReceiveHeader(e);
            }
        }

        /// <summary>
        /// Message의 시작지점인지 판단
        /// </summary>
        /// <param name="rt"></param>
        /// <param name="bytesRead"></param>
        /// <returns></returns>
        private bool IsStartOfMessage(ReadToken rt, int bytesRead)
        {
            return rt.IsInitialized && (_messageSerializer.GetHeaderSize() >= bytesRead);
        }

        /// <summary>
        /// 비동기 Send operation 시작
        /// </summary>
        /// <param name="e"></param>
        /// <param name="bytesToSend"></param>
        private void StartSend(SocketAsyncEventArgs e, int bytesToSend)
        {            
            WriteToken wt = (WriteToken)e.UserToken;

            System.Buffer.BlockCopy(wt.BytesToSend, 0, e.Buffer, wt.BufferOffset, bytesToSend); //TODO Exception Handling
            e.SetBuffer(wt.BufferOffset, bytesToSend);

            bool willRaiseEvent = wt.Socket.SendAsync(e);
            if (!willRaiseEvent)
            {
                ProcessSend(e);
            }
        }

        private void ProcessSend(SocketAsyncEventArgs e)
        {
            Debug.WriteLine("ProcessSend");
            WriteToken wt = (WriteToken)e.UserToken;

            if (e.SocketError != SocketError.Success)
            {
                HandleError(e);
                return;
            }

            if (e.BytesTransferred == 0)
            {
                CloseSocket(e);
                return;
            }

            int nextBufferSizeToSend = wt.IncrementSentLength(e.BytesTransferred);

            if (nextBufferSizeToSend > 0)
            {
                ContinueSend(e, nextBufferSizeToSend);
            }
            else
            {

                if (wt.LoadNextData())
                {                    
                    StartSend(e, wt.NextBufferSizeToSend);
                }
                else
                {
                    wt.Initialize();
                    Debug.WriteLine("FinishSend", "[DEBUG]");                    
                }
            }
        }

        private void ContinueSend(SocketAsyncEventArgs e, int nextBufferSizeToSend)
        {
            WriteToken wt = (WriteToken)e.UserToken;

            System.Buffer.BlockCopy(wt.BytesToSend, wt.TotalCurrentBytesSent, e.Buffer, wt.BufferOffset, nextBufferSizeToSend);
            e.SetBuffer(wt.BufferOffset, nextBufferSizeToSend);

            bool willRaiseEvent = wt.Socket.SendAsync(e);
            if (!willRaiseEvent)
            {
                ProcessSend(e);
            }
        }

        public void Send<T>(int handlerId, T message) where T : TBase
        {
            Debug.WriteLine("HandlerId:" + handlerId);
            if (_saeaWrite == null)
            {
                Debug.WriteLine("Connection has not been established yet.");
                return;
            }
            WriteToken wt = (WriteToken)_saeaWrite.UserToken;

            bool canStartNow = wt.AddToSendQueue(ThriftMessageSerializer.Serialize<T>(handlerId, message));
            if (canStartNow)
            {
                StartSend(_saeaWrite, wt.NextBufferSizeToSend);
            }
        }

        /// <summary>
        /// SocketError에 대한 처리
        /// </summary>
        /// <param name="e"></param>
        private void HandleError(SocketAsyncEventArgs e)
        {
            switch (e.SocketError)
            {
                case SocketError.ConnectionReset: // 원격지 연결이 종료된 경우
                case SocketError.ConnectionAborted:
                case SocketError.ConnectionRefused:
                    CloseSocket(e);
                    break;
                default:                    
                    OnSocketError(this, e.SocketError);
                    break;
            }
        }
    }
}
