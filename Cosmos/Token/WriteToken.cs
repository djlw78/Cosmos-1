using System.Net.Sockets;

namespace Cosmos.Token
{
    public sealed class WriteToken
    {
        SocketAsyncEventArgs _saea;
        readonly int _bufferSize;

        byte[] _bytesToSend = null;
        int _totalCurrentBytesSent = 0; // 전송한 패킷 사이즈              

        public WriteToken(SocketAsyncEventArgs saea, int bufferSize)
        {
            this._saea = saea;
            this._bufferSize = bufferSize;
        }

        public Socket Socket
        {
            get
            {
                return _saea.AcceptSocket;
            }
        }

        public int BufferOffset
        {
            get { return _saea.Offset; }
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
