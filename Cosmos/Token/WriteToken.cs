using System.Collections.Generic;
using System.Net.Sockets;

namespace Cosmos.Token
{
public sealed class WriteToken
{
    SocketAsyncEventArgs _saea;
    readonly int _bufferSize;
    volatile bool _IsWriting = false;


    byte[] _bytesToSend = null;
    int _totalCurrentBytesSent = 0; // 전송한 패킷 사이즈

    Queue<byte[]> _sendQueue = null;

    object item = new object();

    public WriteToken(SocketAsyncEventArgs saea, int bufferSize)
    {
        this._saea = saea;
        this._bufferSize = bufferSize;
        this._sendQueue = new Queue<byte[]>();
    }

    public Socket Socket
    {
        get
        {
            return _saea.AcceptSocket;
        }
    }

    public bool AddToSendQueue(byte[] data)
    {
        if (_IsWriting == false)
        {
            _IsWriting = true;
            _bytesToSend = data;
            return true;
        }
        else
        {
            lock (item)
            {
                _sendQueue.Enqueue(data);
            }
            return false;
        }
    }

    public bool LoadNextData()
    {
        byte[] data = null;

        lock(item)
        {
            if (_sendQueue.Count > 0)
            {
                data = _sendQueue.Dequeue();
            }
        }

        if (data != null)
        {
            _bytesToSend = data;
            _totalCurrentBytesSent = 0;
            return true;
        }
        else
        {
            return false;
        }
    }

    public int BufferOffset
    {
        get
        {
            return _saea.Offset;
        }
    }

    /// <summary>
    /// 현재 보내야 할 전체 바이트 배열
    /// </summary>
    public byte[] BytesToSend
    {
        get
        {
            return _bytesToSend;
        }
        set
        {
            _bytesToSend = value;
        }
    }

    /// <summary>
    /// 현재 까지 보낸 바이트 수
    /// </summary>
    public int TotalCurrentBytesSent
    {
        get
        {
            return _totalCurrentBytesSent;
        }
        set
        {
            _totalCurrentBytesSent = value;
        }
    }

    /// <summary>
    /// 다 보내기 까지 남은 바이트
    /// </summary>
    public int RemainBytesToSend
    {
        get
        {
            return _bytesToSend.Length - _totalCurrentBytesSent;
        }
    }

    /// <summary>
    /// 다음 write 시 다음 읽어올 버퍼 사이즈
    /// </summary>
    public int NextBufferSizeToSend
    {
        get
        {
            if (RemainBytesToSend > _bufferSize)
            {
                return _bufferSize;
            }
            else
            {
                return RemainBytesToSend;
            }
        }
    }

    public void Initialize()
    {
        _IsWriting = false;
        _bytesToSend = null;
        _totalCurrentBytesSent = 0;
    }

    public int IncrementSentLength(int bytesTransferred)
    {
        _totalCurrentBytesSent += bytesTransferred;
        return NextBufferSizeToSend;
    }
}
}
