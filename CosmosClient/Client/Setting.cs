
namespace Cosmos.Client
{
    public class Setting
    {
        readonly int _readBufferSize;
        readonly int _writeBufferSize;
        readonly int _connectionTimeoutInMilliSeconds;

        public Setting(int readBufferSize, int writeBufferSize, int connectionTimeoutInMilliSeconds)
        {
            _readBufferSize = readBufferSize;
            _writeBufferSize = writeBufferSize;
            _connectionTimeoutInMilliSeconds = connectionTimeoutInMilliSeconds;
        }

        public int ReadBufferSize
        {
            get
            {
                return _readBufferSize;
            }
        }

        public int WriteBufferSize
        {
            get
            {
                return _writeBufferSize;
            }
        }

        public int ConnectionTimeoutInMilliSeconds
        {
            get
            {
                return _connectionTimeoutInMilliSeconds;
            }
        }
    }
}
