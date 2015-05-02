using System;
using System.Net.Sockets;
using Thrift.Protocol;

namespace Cosmos.Server
{
public class Session
{
    SocketAsyncEventArgs _saeaWrite;
    int _handlerId;
    byte[] _payload;

    public delegate void MessageWriteEventHandler(SocketAsyncEventArgs saeaWrite, int handlerId, TBase message);
    public event MessageWriteEventHandler OnWrite;

    public delegate void MessageWriteToAllEventHandler(int ignoreSessionId, int handlerId, TBase message);
    public event MessageWriteToAllEventHandler OnWriteToAllExcept;

    public delegate void MessageWriteToEventHandler(int sessionId, int handlerId, TBase message);
    public event MessageWriteToEventHandler OnWriteTo;

    public Session(SocketAsyncEventArgs saeaWrite, int handlerId, byte[] payload)
    {
        _saeaWrite = saeaWrite;
        _handlerId = handlerId;
        _payload = payload;
    }

    public Session(SocketAsyncEventArgs saeaWrite)
    {
        _saeaWrite = saeaWrite;
    }

    public int SessionId
    {
        get
        {
            if (_saeaWrite.AcceptSocket != null)
            {
                return _saeaWrite.AcceptSocket.GetHashCode();
            }
            else
            {
                return -1;
            }
        }
    }

    public int HandlerId
    {
        get
        {
            return _handlerId;
        }
    }

    public byte[] Payload
    {
        get
        {
            return _payload;
        }
    }

    public bool Connected
    {
        get
        {
            if (_saeaWrite.AcceptSocket == null)
            {
                return false;
            }
            else
            {
                return !(_saeaWrite.AcceptSocket.Poll(3000, SelectMode.SelectRead) && _saeaWrite.AcceptSocket.Available == 0);
            }
        }
    }
    public void Write(int handlerId, TBase message)
    {
        if (OnWrite == null) return;
        OnWrite(_saeaWrite, handlerId, message);
    }

    public void WriteToAllExcept(int ignoreSessionId, int handlerId, TBase message)
    {
        if (OnWriteToAllExcept == null) return;
        OnWriteToAllExcept(ignoreSessionId, handlerId, message);
    }

    /// <summary>
    /// session id 에게 메세지를 보낸다.
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="message"></param>
    public void WriteTo(int sessionId, int handlerId, TBase message)
    {
        if (OnWrite == null) return;
        OnWriteTo(sessionId, handlerId, message);
    }
}
}
