
namespace Cosmos.Client
{
    public class Setting
    {
        readonly int _readBufferSize;
        readonly int _writeBufferSize;
        readonly int _writeSimulataneous;

        public Setting(int readBufferSize, int writeBufferSize, int writeSimultaneous)
        {
            _readBufferSize = readBufferSize;
            _writeBufferSize = writeBufferSize;
            _writeSimulataneous = writeSimultaneous;
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

        public int WriteSimultaneous
        {
            get
            {
                return _writeSimulataneous;
            }
        }
    }
}
