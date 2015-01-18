using Cosmos.Buffer;
using Cosmos.Codec;
using Cosmos.Pool;
using Cosmos.Token;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

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

        #region 
        SocketAsyncEventArgsPool _poolWriteSaea;
        #endregion

        #region Event handler
        public delegate void ConnectEventHandler(object sender);
        public delegate void ReadEventHandler(object sender, Session session, object message);
        public delegate void DisconnectEventHandler(object sender);
        public delegate void SocketErrorEventHandler(object sender, SocketError socketError);

        public event ConnectEventHandler OnConnected;
        public event ReadEventHandler OnRead;
        public event DisconnectEventHandler OnDisconnected;
        public event SocketErrorEventHandler OnSocketError;
        #endregion

        public Bootstrap(Setting setting, IMessageSerializer messageSerializer)
        {
            this._setting = setting;
            this._messageSerializer = messageSerializer;

            this._readBufferManager = new BufferManager(setting.ReadBufferSize, 1, _messageSerializer.GetHeaderSize());
            this._writeBufferManager = new BufferManager(setting.WriteBufferSize, setting.WriteSimultaneous, 0);

            this._poolWriteSaea = new SocketAsyncEventArgsPool(setting.WriteSimultaneous);

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

                Trace.Write("Creating " + _setting.WriteSimultaneous + " SocketEventAsyncArgs for write...", "[INFO]");
                for (int i = 0; i < _setting.WriteSimultaneous; ++i)
                {
                    SocketAsyncEventArgs e = new SocketAsyncEventArgs();
                    _writeBufferManager.SetBuffer(e);
                    e.AcceptSocket = (Socket)saeaConnect.UserToken;
                    e.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                    e.UserToken = new WriteToken(e, _setting.WriteBufferSize);
                    _poolWriteSaea.Push(e);
                }
                Trace.WriteLine("Done!");

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
                // Receive 했는데 읽어온 데이터가 없으면 연결 종료 한다.
                //TODO
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
                // 데이터를 성공적으로 읽은경우 MessageReceived를 처리하고 다시 Receive operation을 시작한다.
                object payload = _messageSerializer.Deserialize(rt.TotalData);
                //Debug.WriteLine("Handler ID:{0}, PayLoad:{1}", rt.HandlerId, payload);
                Session session = new Session(rt.HandlerId, payload);
                OnRead(this, session, payload);
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
                // Send 했는데 보낸 데이터가 없으면 연결 종료 한다.
                //CloseClientSocket(e);
                StartReceiveHeader(e);
                return;
            }

            int nextBufferSizeToSend = wt.IncrementSentLength(e.BytesTransferred);

            if (nextBufferSizeToSend > 0)
            {
                ContinueSend(e, nextBufferSizeToSend);
            }
            else
            {
                FinishSend(e);
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

        /// <summary>
        /// 보내기 완료 동작
        /// WriteToken을 초기화 하고 AcceptSocket을 해제 한 후 Pool에 반환한다.
        /// </summary>
        /// <param name="e"></param>
        private void FinishSend(SocketAsyncEventArgs e)
        {
            Debug.WriteLine("FinishSend", "[DEBUG]");
            WriteToken wt = (WriteToken)e.UserToken;
            wt.Initialize();
            _poolWriteSaea.Push(e);            
        }

        public void Send(int handlerId, object message)
        {
            SocketAsyncEventArgs saea = _poolWriteSaea.Pop();

            WriteToken wt = (WriteToken)saea.UserToken;
            wt.BytesToSend = _messageSerializer.Serialize(handlerId, message);

            StartSend(saea, wt.NextBufferSizeToSend);
        }

        public void Send(object message)
        {
            this.Send(0, message);
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
                    OnDisconnected(this);
                    break;
                default:                    
                    OnSocketError(this, e.SocketError);
                    break;
            }
        }
    }
}
