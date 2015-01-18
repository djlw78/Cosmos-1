using System.Net.Sockets;

namespace Cosmos.Token
{
    public sealed class ReadToken
    {
        SocketAsyncEventArgs _saea;
        readonly int _bufferSize;

        #region Offset
        readonly int _headerOffset = 0;
        readonly int _payloadOffset = 0;
        #endregion

        #region Header
        int _totalDataLength = 0;
        int _handlerId = 0;
        #endregion

        #region Stream data
        byte[] _totalData = null;
        int _totalHeaderLength = 0;
        int _totalProcessedDataLength = 0;
        #endregion

        public ReadToken(SocketAsyncEventArgs e, int bufferSize, int headerSize)
        {
            _saea = e;
            _headerOffset = e.Offset;
            _payloadOffset = _headerOffset + headerSize;
            _bufferSize = bufferSize;
        }

        public Socket Socket
        {
            get
            {
                return _saea.AcceptSocket;
            }
        }

        /// <summary>
        /// 전체 데이터 길이
        /// </summary>
        public int TotalDataLength
        {
            get { return _totalDataLength; }
            set
            {
                _totalDataLength = value;
            }
        }

        public int HandlerId
        {
            get { return _handlerId; }
            set
            {
                _handlerId = value;
            }
        }

        public byte[] TotalData
        {
            get { return _totalData; }
        }

        public void AssignTotalData(int size)
        {
            _totalData = new byte[size];
        }

        public int TotalHeaderLength
        {
            get { return _totalDataLength; }
            set { _totalDataLength = value; }
        }

        public int TotalProcessedDataLength
        {
            get { return _totalProcessedDataLength; }
            set { _totalProcessedDataLength = value; }
        }

        public int HeaderOffset
        {
            get { return _headerOffset; }
        }

        public int PayloadOffset
        {
            get { return _payloadOffset; }
        }

        public int RemainBytesToReceive
        {
            get
            {
                return _totalDataLength - _totalProcessedDataLength;
            }
        }

        public bool IsInitialized
        {
            get
            {
                return _totalData == null && _totalDataLength == 0;
            }
        }

        /// <summary>
        /// operation을 위해 초기화 할 변수 들을 초기화 한다.
        /// </summary>
        public void Initialize()
        {
            _totalDataLength = 0;
            _totalHeaderLength = 0;
            _totalProcessedDataLength = 0;
            _totalData = null;
        }

        /// <summary>
        /// Continue R/W 시 다음 읽어올 버퍼 사이즈
        /// </summary>
        public int NextBufferSizeToReceive
        {
            get
            {
                if (RemainBytesToReceive > _bufferSize)
                {
                    return _bufferSize;
                }
                else
                {
                    return RemainBytesToReceive;
                }
            }
        }

        /// <summary>
        /// 받아온 byte array를 Session 의 byte array에 더해준다.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="bytesRead"></param>
        /// <returns>다음 읽어올 버퍼사이즈</returns>
        public int AddTotalData()
        {
            System.Buffer.BlockCopy(_saea.Buffer, _saea.Offset, _totalData, _totalProcessedDataLength, _saea.BytesTransferred);
            _totalProcessedDataLength += _saea.BytesTransferred;
            return NextBufferSizeToReceive;
        }


        /// <summary>
        /// receive header length 를 늘려준다.
        /// </summary>
        /// <returns></returns>
        public int IncrementHeaderLength()
        {
            _totalHeaderLength += _saea.BytesTransferred;
            return _totalHeaderLength;
        }
    }
}
