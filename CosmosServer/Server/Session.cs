using System.Net.Sockets;

namespace Cosmos.Server
{
public class Session
{
    SocketAsyncEventArgs _saeaWrite;
    int _handlerId;
    byte[] _payload;

    public delegate void MessageWriteEventHandler(SocketAsyncEventArgs saeaWrite, byte[] payload);
    public event MessageWriteEventHandler OnWrite;

    public delegate void MessageWriteToAllEventHandler(int ignoreSessionId, byte[] payload);
    public event MessageWriteToAllEventHandler OnWriteToAllExcept;

    public delegate void MessageWriteToEventHandler(int sessionId, byte[] payload);
    public event MessageWriteToEventHandler OnWriteTo;

    public Session(SocketAsyncEventArgs saeaWrite, ushort handlerId, byte[] payload)
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
            return _saeaWrite.AcceptSocket.GetHashCode();
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

    public void Write(byte[] payload)
    {
        OnWrite(_saeaWrite, payload);
    }

    public void WriteToAllExceptSelf(byte[] payload)
    {
        WriteToAllExcept(SessionId, payload);
    }

    public void WriteToAllExcept(int ignoreSessionId, byte[] payload)
    {
        OnWriteToAllExcept(ignoreSessionId, payload);
    }

    /// <summary>
    /// session id 에게 메세지를 보낸다.
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="message"></param>
    public void WriteTo(int sessionId, byte[] payload)
    {
        OnWriteTo(sessionId, payload);
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
}
}
