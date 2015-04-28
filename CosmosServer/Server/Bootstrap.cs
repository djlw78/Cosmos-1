using Cosmos.Buffer;
using Cosmos.Codec;
using Cosmos.Token;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using CosmosServer.Token;
using CosmosServer.Pool;

namespace Cosmos.Server
{
public class Bootstrap
{
    #region Immutable construction member variables

    readonly Setting _setting;

    readonly IMessageSerializer _messageSerializer;

    //a large reusable set of buffers for all socket operations.
    readonly BufferManager _readBufferManager;
    readonly BufferManager _writeBufferManager;

    #endregion

    #region Member variables
    int _numberOfConnections = 0;

    // the socket used to listen for incoming connection requests
    Socket _listenSocket;

    // pool of reusable SocketAsyncEventArgs objects for accept
    SocketAsyncEventArgsPool _poolAcceptEventArgs;

    // pool of reusable SocketAsyncEventArgs objects for read
    SocketAsyncEventArgsPool _poolReadEventArgs;

    // pool of reusable SocketAsyncEventArgs objects for write
    SocketAsyncEventArgsPool _poolWriteEventArgs;

    // the total number of clients connected to the server
    Semaphore _theMaxConnectionsEnforcer;

    ConcurrentDictionary<int, SocketAsyncEventArgs> _channels;

    #endregion

    #region Event Handlers

    public delegate void ReadEventHandler(Session session);
    public delegate void AcceptedEventHandler(Socket socket);
    public delegate void ClosedEventHandler(Session session, Socket socket);
    public delegate void SocketErrorEventHandler(SocketError socketError);

    public event ReadEventHandler OnRead;
    public event AcceptedEventHandler OnAccepted;
    public event ClosedEventHandler OnClosed;
    public event SocketErrorEventHandler OnSocketError;

    #endregion

    public Bootstrap(Setting setting, IMessageSerializer messageSerializer)
    {
        this._setting = setting;
        this._messageSerializer = messageSerializer;

        this._readBufferManager = new BufferManager(setting.ReceiveBufferSize, setting.MaxConnections, _messageSerializer.GetHeaderSize());
        this._writeBufferManager = new BufferManager(setting.SendBufferSize, setting.MaxConnections, 0);

        this._poolAcceptEventArgs = new SocketAsyncEventArgsPool(setting.MaxSimulateneousAccepts);
        this._poolReadEventArgs = new SocketAsyncEventArgsPool(setting.MaxConnections);
        this._poolWriteEventArgs = new SocketAsyncEventArgsPool(setting.MaxConnections);

        this._theMaxConnectionsEnforcer = new Semaphore(setting.MaxConnections + 1, setting.MaxConnections + 1);

        this._channels = new ConcurrentDictionary<int, SocketAsyncEventArgs>();
        Init();
    }

    /// <summary>
    /// 클래스를 초기화 해준다.
    /// 필요한 객체들을 생성한다.
    /// </summary>
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

        Trace.Write("Creating " + _setting.MaxSimulateneousAccepts + " SocketEventAsyncArgs for accepting...", "[INFO]");
        for (int i = 0; i < this._setting.MaxSimulateneousAccepts; i++)
        {
            _poolAcceptEventArgs.Push(CreateSocketAsyncEventArgsForAccept());
        }
        Trace.WriteLine("Done!");

        Trace.Write("Creating " + _setting.MaxConnections + " SocketEventAsyncArgs for read and write...", "[INFO]");
        for (int i = 0; i < this._setting.MaxConnections; ++i)
        {
            SocketAsyncEventArgs saeaRead = new SocketAsyncEventArgs();
            _readBufferManager.SetBuffer(saeaRead);
            saeaRead.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
            saeaRead.UserToken = new ReadToken(saeaRead, _setting.ReceiveBufferSize, _messageSerializer.GetHeaderSize());
            _poolReadEventArgs.Push(saeaRead);


            SocketAsyncEventArgs saeaWrite = new SocketAsyncEventArgs();
            _writeBufferManager.SetBuffer(saeaWrite);
            saeaWrite.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
            saeaWrite.UserToken = new ConcurrentWriteToken(saeaWrite, _setting.SendBufferSize);
            _poolWriteEventArgs.Push(saeaWrite);

        }
        Trace.WriteLine("Done!");
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
    /// 클라이언트로부터의 접속 요청에 대한 처리 동작을 시작한다.
    /// </summary>
    private void StartAccept()
    {
        SocketAsyncEventArgs acceptEventArg;

        if (_poolAcceptEventArgs.Count > 1)
        {
            try
            {
                Debug.WriteLine("Pop accept SAEA from the pool", "[DEBUG]");
                acceptEventArg = this._poolAcceptEventArgs.Pop();
            }
            catch
            {
                Debug.WriteLine("Create new accept SAEA - catch", "[DEBUG]");
                acceptEventArg = CreateSocketAsyncEventArgsForAccept();
            }
        }
        else
        {
            Debug.WriteLine("Create new accept SAEA", "[DEBUG]");
            acceptEventArg = CreateSocketAsyncEventArgsForAccept();
        }


        Debug.Write("Check max simulatenous connections...", "[DEBUG]");
        _theMaxConnectionsEnforcer.WaitOne();        // 최대 동시 접속자를 제한 하기 위한 Semaphore 최대에 도달한 경우 쓰레드를 블럭하고 Release가 호출될 때 까지 기다린다.
        Debug.WriteLine("OK!");

        //커넥션을 받아들이는 동작을 비동기적으로 시작한다.
        bool willRaiseEvent = _listenSocket.AcceptAsync(acceptEventArg);

        // I/O operation이 동기적으로 수행되었을 경우에 false를 리턴하고 ProcessAccept를 직접 호출해줘야 한다. 비동기로 완료되었으면 내부적으로 ProcessAccept를 호출한다.
        if (!willRaiseEvent)
        {
            ProcessAccept(acceptEventArg);
        }
    }

    /// <summary>
    /// Accpet 동작을 수행할 SocketEventArgs를 생성한다.
    /// </summary>
    /// <returns></returns>
    private SocketAsyncEventArgs CreateSocketAsyncEventArgsForAccept()
    {
        SocketAsyncEventArgs acceptEventArg = new SocketAsyncEventArgs();
        acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
        return acceptEventArg;
    }

    /// <summary>
    /// SocketAsyncEventArgs 객체의 Accept 동작이 완료 된 후에 호출되는 콜백
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
    {
        ProcessAccept(e);
    }

    /// <summary>
    /// Accept가 끝난 후에 처리해야 할 것들을 처리한다.
    /// </summary>
    /// <param name="acceptSocketEventArgs"></param>
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

        //RW를 위한 SAEA를 Pool 에서 가지고 온다.
        SocketAsyncEventArgs saeaRead = this._poolReadEventArgs.Pop();
        saeaRead.AcceptSocket = acceptSocketEventArgs.AcceptSocket;
        ReadToken rt = ((ReadToken)saeaRead.UserToken);

        SocketAsyncEventArgs saeaWrite = this._poolWriteEventArgs.Pop();
        saeaWrite.AcceptSocket = acceptSocketEventArgs.AcceptSocket;
        rt.WriteSaea = saeaWrite;

        // 채널컨테이너에 새로운 소켓 추가
        _channels.TryAdd(rt.Socket.GetHashCode(), saeaWrite);
        OnAccepted(saeaRead.AcceptSocket);

        // Accept SAEA는 풀에 반환한다.
        acceptSocketEventArgs.AcceptSocket = null;
        this._poolAcceptEventArgs.Push(acceptSocketEventArgs);

        // Receive 시작
        StartReceiveHeader(saeaRead);
    }

    /// <summary>
    /// Accept 중 소켓 에러가 발생하면 Accept SAEA를 풀에 반환하고 다시 Accept 시작
    /// </summary>
    /// <param name="e"></param>
    private void HandleBadAccept(SocketAsyncEventArgs e)
    {
        e.AcceptSocket.Close();
        e.AcceptSocket = null;
        _poolAcceptEventArgs.Push(e);
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
        SocketAsyncEventArgs saea;
        _channels.TryRemove(socket.GetHashCode(), out saea);

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
            _poolReadEventArgs.Push(e);

            Debug.WriteLine("Returning Write SAEA to pool", "[DEBUG]");
            SocketAsyncEventArgs writeSaea = rt.WriteSaea;
            if (writeSaea != null)
            {
                writeSaea.AcceptSocket = null;
                _poolWriteEventArgs.Push(writeSaea);
            }
        }
        else if (e.UserToken is ConcurrentWriteToken)
        {
            Trace.WriteLine("Returning Write SAEA to pool", "[INFO]");
            ConcurrentWriteToken wt = (ConcurrentWriteToken)e.UserToken;
            e.AcceptSocket = null;
            _poolWriteEventArgs.Push(e);
        }
        else
        {
            return;
        }

        _theMaxConnectionsEnforcer.Release();
        Debug.WriteLine("OUT! Current connections:" + Interlocked.Decrement(ref _numberOfConnections), "[DEBUG]");
    }

    /// <summary>
    /// 새로운 소켓 Receive 동작을 비동기적으로 시작한다.
    /// </summary>
    /// <param name="e"></param>
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
    /// Receive 처리
    /// </summary>
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
            CloseClientSocket(e);
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

            int totalDataLength;
            ushort handlerId;

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
            Debug.WriteLine("Handler ID:{0}", rt.HandlerId);
            Session session = new Session(rt.WriteSaea, rt.HandlerId, rt.TotalData);
            session.OnWrite += OnWrite;
            session.OnWriteTo += OnWriteTo;
            session.OnWriteToAllExcept += OnWriteToAllExcept;
            OnRead(session);
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
            CloseClientSocket(e);
            break;
        default:
            OnSocketError(e.SocketError);
            break;
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

    /// <summary>
    /// OS로 부터 IO 완료 통지를 받은 후 처리
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void IO_Completed(object sender, SocketAsyncEventArgs e)
    {
        switch (e.LastOperation)
        {
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
    /// 비동기 Send operation 시작
    /// </summary>
    /// <param name="e"></param>
    /// <param name="bytesToSend"></param>
    private void StartSend(SocketAsyncEventArgs e, int bytesToSend)
    {
        Debug.WriteLine("StartSend-BytesToSend:" + bytesToSend, "[DEBUG]");
        ConcurrentWriteToken wt = (ConcurrentWriteToken)e.UserToken;

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
        Debug.WriteLine("ProcessSend","[DEBUG]");
        ConcurrentWriteToken wt = (ConcurrentWriteToken)e.UserToken;

        if (e.SocketError != SocketError.Success)
        {
            HandleError(e);
            return;
        }

        if (e.BytesTransferred == 0)
        {
            // Send 했는데 보낸 데이터가 없으면 연결 종료 한다.
            CloseClientSocket(e);
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
        Debug.WriteLine("ContinueSend-nextBufferSizeToSend:" + nextBufferSizeToSend, "[DEBUG]");
        ConcurrentWriteToken wt = (ConcurrentWriteToken)e.UserToken;

        System.Buffer.BlockCopy(wt.BytesToSend, wt.TotalCurrentBytesSent, e.Buffer, wt.BufferOffset, nextBufferSizeToSend);
        e.SetBuffer(wt.BufferOffset, nextBufferSizeToSend);

        bool willRaiseEvent = wt.Socket.SendAsync(e);
        if (!willRaiseEvent)
        {
            ProcessSend(e);
        }
    }

    /// <summary>
    /// Session 객체에서 Send 요청을 했을 때
    /// </summary>
    /// <param name="socket"></param>
    /// <param name="handlerId"></param>
    /// <param name="message"></param>
    private void OnWrite<T>(SocketAsyncEventArgs saeaWrite, ushort handlerId, T message)
    {
        Debug.WriteLine("OnWrite:HandlerId:" + handlerId +", Payload:" + message,"[DEBUG]");
        ConcurrentWriteToken wt = (ConcurrentWriteToken)saeaWrite.UserToken;

        bool canStartNow = wt.AddToSendQueue(_messageSerializer.Serialize<T>(handlerId, message));
        if (canStartNow)
        {
            StartSend(saeaWrite, wt.NextBufferSizeToSend);
        }
    }

    /// <summary>
    /// Sessino 객체에서 특정 sessionId로 Send 요청을 했을 때
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="handlerId"></param>
    /// <param name="message"></param>
    private void OnWriteTo<T>(int sessionId, ushort handlerId, T message)
    {
        SocketAsyncEventArgs saea;
        if (_channels.TryGetValue(sessionId, out saea))
        {
            OnWrite(saea, handlerId, message);
        }
    }

    /// <summary>
    /// Session 객체에서 ignoreSessionId를 제외한 전체 Session에게 메세지를 보낼 때
    /// </summary>
    /// <param name="ignoreSessionId"></param>
    /// <param name="handlerId"></param>
    /// <param name="message"></param>
    private void OnWriteToAllExcept<T>(int ignoreSessionId, ushort handlerId, T message)
    {
        IEnumerator<SocketAsyncEventArgs> enumerator = _channels.Values.GetEnumerator();
        while (enumerator.MoveNext())
        {
            SocketAsyncEventArgs s = enumerator.Current;
            if (s.AcceptSocket.GetHashCode().Equals(ignoreSessionId) == false)
            {
                OnWrite<T>(s, handlerId, message);
            }
        }
    }

    /// <summary>
    /// 전체 Session에게 메세지를 보낼 때
    /// </summary>
    /// <param name="ignoreSessionId"></param>
    /// <param name="handlerId"></param>
    /// <param name="message"></param>
    private void OnWriteToAll<T>(ushort handlerId, T message)
    {
        IEnumerator<SocketAsyncEventArgs> enumerator = _channels.Values.GetEnumerator();
        while (enumerator.MoveNext())
        {
            SocketAsyncEventArgs s = enumerator.Current;
            OnWrite<T>(s, handlerId, message);
        }
    }

    /// <summary>
    /// Shutdown the server. it's not graceful yet.
    /// </summary>
    public void Shutdown()
    {
        IEnumerator<SocketAsyncEventArgs> enumerator = _channels.Values.GetEnumerator();
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
}
}
