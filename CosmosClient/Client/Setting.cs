
namespace Cosmos.Client
{
    public class Setting
    {
        readonly int _readBufferSize;
        readonly int _writeBufferSize;

        public Setting(int readBufferSize, int writeBufferSize)
        {
            _readBufferSize = readBufferSize;
            _writeBufferSize = writeBufferSize;
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
    }
}
