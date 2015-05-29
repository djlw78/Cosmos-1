using Cosmos.Buffer;
using Cosmos.Codec;
using Cosmos.Token;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Cosmos.Pool;

namespace Cosmos.Server
{
    public class Bootstrap
    {
        #region Immutable construction member variables

        readonly Setting _setting;

        readonly BaseMessageSerializer _messageSerializer;

        //a large reusable set of buffers for all socket operations.
        readonly BufferManager _readBufferManager;
        readonly BufferManager _writeBufferManager;

        #endregion

        #region Member variables
        int _numberOfConnections = 0; //제일 첫번 째 Accept

        // the socket used to listen for incoming connection requests
        Socket _listenSocket;

        // pool of reusable SocketAsyncEventArgs objects for accept
        DefaultSaeaPool _poolAcceptEventArgs;

        // pool of reusable SocketAsyncEventArgs objects for read
        SearchableSaeaPool _poolReadEventArgs;

        // pool of reusable SocketAsyncEventArgs objects for write
        SearchableSaeaPool _poolWriteEventArgs;

        // the total number of clients connected to the server
        Semaphore _theMaxConnectionsEnforcer;
        #endregion

        #region Event Handlers

        public delegate void ReadEventHandler(Session session, ushort handlerId, byte[] payload);
        public delegate void AcceptedEventHandler(Socket socket);
        public delegate void ClosedEventHandler(Session session, Socket socket);
        public delegate void SocketErrorEventHandler(SocketError socketError);

        public event ReadEventHandler OnRead;
        public event AcceptedEventHandler OnAccepted;
        public event ClosedEventHandler OnClosed;
        public event SocketErrorEventHandler OnSocketError;

        #endregion

        public Bootstrap(Setting setting, BaseMessageSerializer messageSerializer)
        {
            this._setting = setting;
            this._messageSerializer = messageSerializer;

            this._readBufferManager = new BufferManager(setting.ReceiveBufferSize, setting.MaxConnections, BaseMessageSerializer._MESSAGE_HEADER_SIZE);
            this._writeBufferManager = new BufferManager(setting.SendBufferSize, setting.MaxConnections, 0);

            this._theMaxConnectionsEnforcer = new Semaphore(setting.MaxConnections, setting.MaxConnections);
            Init();
        }

        private void Init()
        {
            Trace.WriteLine("Back Log:" + _setting.BackLog, "[INFO]");
            Trace.WriteLine("Max Connections:" + _setting.MaxConnections, "[INFO]");
            Trace.WriteLine("Receive Buffer Size:" + _setting.ReceiveBufferSize, "[INFO]");
            Trace.WriteLine("Send Buffer Size:" + _setting.SendBufferSize, "[INFO]");
            Trace.WriteLine("Max Accepts Simultaneously:" + _setting.MaxSimulateneousAccepts, "[INFO]");

            // memory fragmentation 방지를 위한 공용으로 쓸 큰 버퍼를 생성한다.
            Trace.Write("Creating huge buffer block...", "[INFO]");
            _readBufferManager.InitBuffer();
            _writeBufferManager.InitBuffer();
            Trace.WriteLine("Done!");

            Trace.Write("Creating SocketAsyncEventArgs Pools", "[INFO]");
            this._poolAcceptEventArgs = new DefaultSaeaPool(_setting.MaxSimulateneousAccepts, _setting, new EventHandler<SocketAsyncEventArgs>(IO_Completed));
            this._poolReadEventArgs = new SearchableSaeaPool(_setting.MaxConnections, _setting, _readBufferManager, new EventHandler<SocketAsyncEventArgs>(IO_Completed), SearchableSaeaPool.TokenType.READ);
            this._poolWriteEventArgs = new SearchableSaeaPool(_setting.MaxConnections, _setting, _writeBufferManager, new EventHandler<SocketAsyncEventArgs>(IO_Completed), SearchableSaeaPool.TokenType.WRITE);

            Trace.WriteLine("Done!");
        }

        private void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    ProcessAccept(e);
                    break;
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        /// <summary>
        /// 서버를 시작한다.
        /// </summary>
        public void Start()
        {
            _listenSocket = new Socket(this._setting.LocalEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(this._setting.LocalEndPoint);
            _listenSocket.Listen(this._setting.BackLog);            

            Trace.WriteLine("Socket is listening on " + this._setting.LocalEndPoint.ToString(), "[INFO]");

            Debug.WriteLine("Start accepting...", "[DEBUG]");
            StartAccept();

            MessageWriter.OnWriteTo += OnWriteTo;
            MessageWriter.OnWriteToAll += OnWriteToAll;
        }

        /// <summary>
        /// Shutdown the server. it's not graceful yet.
        /// </summary>
        public void Shutdown()
        {
            IEnumerator<SocketAsyncEventArgs> enumerator = _poolWriteEventArgs.GetBorrowingObjects();
            while (enumerator.MoveNext())
            {
                SocketAsyncEventArgs s = enumerator.Current;
                if (s != null && s.AcceptSocket.Connected == true)
                {
                    s.AcceptSocket.Shutdown(SocketShutdown.Both);
                    s.AcceptSocket.Close();
                }
            }
        }

        private void StartAccept()
        {
            SocketAsyncEventArgs acceptEventArg;

            if (this._poolAcceptEventArgs.Borrow(out acceptEventArg))
            {
                Debug.WriteLine("Pop accept SAEA from the pool id:" + acceptEventArg.GetHashCode(), "[DEBUG]");
            }
            else
            {
                Debug.WriteLine("Pop accept SAEA from the pool - Failed", "[DEBUG]");
                return;
            }

            Debug.Write("Check max simulatenous connections... ", "[DEBUG]");
            _theMaxConnectionsEnforcer.WaitOne();    // 최대 동시 접속자를 제한 하기 위한 Semaphore 최대에 도달한 경우 쓰레드를 블럭하고 Release가 호출될 때 까지 기다린다.
            Debug.WriteLine("OK!");

            //커넥션을 받아들이는 동작을 비동기적으로 시작한다.
            bool willRaiseEvent = _listenSocket.AcceptAsync(acceptEventArg);
           
            // I/O operation이 동기적으로 수행되었을 경우에 false를 리턴하고 ProcessAccept를 직접 호출해줘야 한다. 비동기로 완료되었으면 내부적으로 ProcessAccept를 호출한다.
            if (!willRaiseEvent)
            {
                ProcessAccept(acceptEventArg);
            }
        }

        private void ProcessAccept(SocketAsyncEventArgs acceptSocketEventArgs)
        {
            if (acceptSocketEventArgs.SocketError != SocketError.Success)
            {
                Debug.WriteLine("Accept failed!.", "[ERROR]");
                StartAccept();
                HandleBadAccept(acceptSocketEventArgs);
                return;
            }
            StartAccept(); // 새로운 Aceept를 시작한다.
            Debug.WriteLine("IN! Current connections:" + Interlocked.Increment(ref _numberOfConnections), "[DEBUG]");

            int socketId = acceptSocketEventArgs.AcceptSocket.GetHashCode();
            //RW를 위한 SAEA를 Pool 에서 가지고 온다.
            SocketAsyncEventArgs saeaRead;
            if (this._poolReadEventArgs.Borrow(socketId, out saeaRead))
            {
                saeaRead.AcceptSocket = acceptSocketEventArgs.AcceptSocket;
                ReadToken rt = ((ReadToken)saeaRead.UserToken);

                SocketAsyncEventArgs saeaWrite;
                if (this._poolWriteEventArgs.Borrow(socketId, out saeaWrite))
                {
                    saeaWrite.AcceptSocket = acceptSocketEventArgs.AcceptSocket;
                    rt.WriteSaea = saeaWrite;

                    OnAccepted(saeaRead.AcceptSocket);
                    // Receive 시작
                    StartReceiveHeader(saeaRead);
                }
                else
                {
                    saeaRead.AcceptSocket = null;
                    saeaRead.UserToken = null;
                    _poolReadEventArgs.Return(socketId);
                }
            }
            else
            {
                Trace.TraceWarning("Failed to borrow");
            }

            // Accept SAEA는 풀에 반환한다.
            acceptSocketEventArgs.AcceptSocket = null;
            _poolAcceptEventArgs.Return(acceptSocketEventArgs);

            
        }

        private void StartReceiveHeader(SocketAsyncEventArgs saeaRead)
        {
            Debug.WriteLine("StartReceiveHeader", "[DEBUG]");
            ReadToken rt = (ReadToken)saeaRead.UserToken;
            rt.Initialize();

            saeaRead.SetBuffer(rt.HeaderOffset, BaseMessageSerializer._MESSAGE_HEADER_SIZE);   // 최초에는 MessageHeaderSize 만큼만 읽는다.
            bool willRaiseEvent = rt.Socket.ReceiveAsync(saeaRead);
            if (!willRaiseEvent)
            {
                ProcessReceive(saeaRead);
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs saeaRead)
        {
            ReadToken rt = (ReadToken)saeaRead.UserToken;

            if (saeaRead.SocketError != SocketError.Success)
            {
                HandleError(saeaRead);
                return;
            }

            if (saeaRead.BytesTransferred == 0)
            {
                // Receive 했는데 읽어온 데이터가 없으면 연결 종료 한다.
                CloseClientSocket(saeaRead);
                return;
            }

            int nextBufferSizeToRead = 0;

            if (IsStartOfMessage(rt, saeaRead.BytesTransferred)) // 메세지의 시작 부분일 경우
            {
                int totalHeaderReceived = rt.IncrementHeaderLength();

                // 헤더의 길이가 충분하지 않은 경우
                if (totalHeaderReceived < BaseMessageSerializer._MESSAGE_HEADER_SIZE)
                {
                    nextBufferSizeToRead = BaseMessageSerializer._MESSAGE_HEADER_SIZE - totalHeaderReceived;
                    ContinueReceiveHeader(saeaRead, saeaRead.BytesTransferred, nextBufferSizeToRead);
                    return;
                }

                int totalDataLength;
                ushort handlerId;

                // 헤더 정보가 올바른지 검증
                if (_messageSerializer.CheckHaderValidation(saeaRead.Buffer, rt.HeaderOffset, out totalDataLength, out handlerId))
                {
                    rt.TotalDataLength = totalDataLength;
                    rt.HandlerId = handlerId;
                    rt.AssignTotalData(totalDataLength);

                    nextBufferSizeToRead = rt.NextBufferSizeToReceive;
                }
                else
                {
                    StartReceiveHeader(saeaRead); // 헤더가 올바르지 않으면 다시 Receive 시작
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
                ContinueReceive(saeaRead, nextBufferSizeToRead);
            }
            else
            {
                // 데이터를 성공적으로 읽은경우 MessageReceived를 처리하고 다시 Receive operation을 시작한다.
                Debug.WriteLine("Handler ID:{0}", rt.HandlerId);
                Session session = new Session(rt.WriteSaea);
                session.OnWrite += OnWrite;
                session.OnWriteTo += OnWriteTo;
                session.OnWriteToAllExcept += OnWriteToAllExcept;
                OnRead(session, rt.HandlerId, rt.TotalData);
                StartReceiveHeader(saeaRead);
            }
        }



        private void ContinueReceive(SocketAsyncEventArgs saeaRead, int nextBufferSizeToRead)
        {
            ReadToken rt = (ReadToken)saeaRead.UserToken;
            saeaRead.SetBuffer(saeaRead.Offset, nextBufferSizeToRead);

            bool willRaiseEvent = rt.Socket.ReceiveAsync(saeaRead);
            if (!willRaiseEvent)
            {
                ProcessReceive(saeaRead);
            }
        }

        private void HandleError(SocketAsyncEventArgs saeaRead)
        {
            switch (saeaRead.SocketError)
            {
                case SocketError.ConnectionReset: // 원격지 연결이 종료된 경우
                case SocketError.ConnectionAborted:
                case SocketError.ConnectionRefused:
                    CloseClientSocket(saeaRead);
                    break;
                default:
                    OnSocketError(saeaRead.SocketError);
                    break;
            }
        }

        /// <summary>
        /// 원격 클라이언트의 접속이 끊겼을 때에 대한 처리
        /// </summary>
        /// <param name="e"></param>
        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            Debug.WriteLine("CloseClientSocket", "[DEBUG]");
            if (e == null || e.AcceptSocket == null) return;

            Socket socket = e.AcceptSocket;

            Session session = new Session(e);
            session.OnWriteTo += OnWriteTo;
            session.OnWriteToAllExcept += OnWriteToAllExcept;

            OnClosed(session, socket);

            if (socket != null && socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            if (e.UserToken is ReadToken)
            {
                Debug.WriteLine("Returning Read SAEA to pool", "[DEBUG]");
                ReadToken rt = (ReadToken)e.UserToken;
                e.AcceptSocket = null;
                _poolReadEventArgs.Return(socket.GetHashCode());

                Debug.WriteLine("Returning Write SAEA to pool", "[DEBUG]");
                SocketAsyncEventArgs writeSaea = rt.WriteSaea;
                if (writeSaea != null)
                {
                    writeSaea.AcceptSocket = null;
                    _poolWriteEventArgs.Return(socket.GetHashCode());
                }
            }
            else if (e.UserToken is WriteToken)
            {
                Trace.WriteLine("Returning Write SAEA to pool", "[INFO]");
                WriteToken wt = (WriteToken)e.UserToken;
                e.AcceptSocket = null;
                _poolWriteEventArgs.Return(socket.GetHashCode());
            }
            else
            {
                return;
            }

            _theMaxConnectionsEnforcer.Release();
            Debug.WriteLine("OUT! Current connections:" + Interlocked.Decrement(ref _numberOfConnections), "[DEBUG]");
        }

        private void ContinueReceiveHeader(SocketAsyncEventArgs saeaRead, int bytesRead, int nextBufferSizeToRead)
        {
            ReadToken rt = (ReadToken)saeaRead.UserToken;
            saeaRead.SetBuffer(rt.HeaderOffset + bytesRead, nextBufferSizeToRead);

            bool willRaiseEvent = rt.Socket.ReceiveAsync(saeaRead);
            if (!willRaiseEvent)
            {
                ProcessReceive(saeaRead);
            }
        }

        private bool IsStartOfMessage(ReadToken rt, int bytesRead)
        {
            return rt.IsInitialized && (BaseMessageSerializer._MESSAGE_HEADER_SIZE >= bytesRead);
        }

        private void HandleBadAccept(SocketAsyncEventArgs acceptSocketEventArgs)
        {
            acceptSocketEventArgs.AcceptSocket.Close();
            acceptSocketEventArgs.AcceptSocket = null;
            _poolAcceptEventArgs.Return(acceptSocketEventArgs);
        }

        private void ProcessSend(SocketAsyncEventArgs saeaWrite)
        {
            Debug.WriteLine("ProcessSend", "[DEBUG]");
            WriteToken wt = (WriteToken)saeaWrite.UserToken;

            if (saeaWrite.SocketError != SocketError.Success)
            {
                HandleError(saeaWrite);
                return;
            }

            if (saeaWrite.BytesTransferred == 0)
            {
                // Send 했는데 보낸 데이터가 없으면 연결 종료 한다.
                CloseClientSocket(saeaWrite);
                return;
            }

            int nextBufferSizeToSend = wt.IncrementSentLength(saeaWrite.BytesTransferred);

            if (nextBufferSizeToSend > 0)
            {
                ContinueSend(saeaWrite, nextBufferSizeToSend);
            }
            else
            {
                if (wt.LoadNextData())
                {
                    StartSend(saeaWrite, wt.NextBufferSizeToSend);
                }
                else
                {
                    wt.Initialize();
                    Debug.WriteLine("FinishSend", "[DEBUG]");
                }
            }
        }

        private void ContinueSend(SocketAsyncEventArgs saeaWrite, int nextBufferSizeToSend)
        {
            Debug.WriteLine("ContinueSend-nextBufferSizeToSend:" + nextBufferSizeToSend, "[DEBUG]");
            WriteToken wt = (WriteToken)saeaWrite.UserToken;

            System.Buffer.BlockCopy(wt.BytesToSend, wt.TotalCurrentBytesSent, saeaWrite.Buffer, wt.BufferOffset, nextBufferSizeToSend);
            saeaWrite.SetBuffer(wt.BufferOffset, nextBufferSizeToSend);

            bool willRaiseEvent = wt.Socket.SendAsync(saeaWrite);
            if (!willRaiseEvent)
            {
                ProcessSend(saeaWrite);
            }
        }

        private void StartSend(SocketAsyncEventArgs saeaWrite, int bytesToSend)
        {
            Debug.WriteLine("StartSend-BytesToSend:" + bytesToSend, "[DEBUG]");
            WriteToken wt = (WriteToken)saeaWrite.UserToken;

            System.Buffer.BlockCopy(wt.BytesToSend, 0, saeaWrite.Buffer, wt.BufferOffset, bytesToSend); //TODO Exception Handling
            saeaWrite.SetBuffer(wt.BufferOffset, bytesToSend);

            bool willRaiseEvent = wt.Socket.SendAsync(saeaWrite);
            if (!willRaiseEvent)
            {
                ProcessSend(saeaWrite);
            }
        }

        private void OnWrite(SocketAsyncEventArgs saeaWrite, byte[] payload)
        {
            WriteToken wt = (WriteToken)saeaWrite.UserToken;

            bool canStartNow = wt.AddToSendQueue(payload);
            if (canStartNow)
            {
                StartSend(saeaWrite, wt.NextBufferSizeToSend);
            }
        }

        private void OnWriteTo(int sessionId, byte[] payload)
        {
            SocketAsyncEventArgs saea;
            if (_poolWriteEventArgs.GetBorrowingObject(sessionId, out saea))
            {
                OnWrite(saea, payload);
            }
        }

        private void OnWriteToAll(byte[] payload)
        {
            IEnumerator<SocketAsyncEventArgs> enumerator = _poolWriteEventArgs.GetBorrowingObjects();
            while (enumerator.MoveNext())
            {
                SocketAsyncEventArgs s = enumerator.Current;
                OnWrite(s, payload);
            }
        }

        private void OnWriteToAllExcept(int ignoreSessionId, byte[] payload)
        {
            IEnumerator<SocketAsyncEventArgs> enumerator = _poolWriteEventArgs.GetBorrowingObjects();
            while (enumerator.MoveNext())
            {
                SocketAsyncEventArgs s = enumerator.Current;
                if (s.AcceptSocket.GetHashCode().Equals(ignoreSessionId) == false)
                {
                    OnWrite(s, payload);
                }
            }
        }
    }
}
