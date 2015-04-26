using System.Net;

namespace Cosmos.Server
{
    public class Setting
    {
        private readonly IPEndPoint _localEndPoint;
        private readonly int _backLog;
        private readonly int _maxConnections;
        private readonly int _maxSimultaneousAceepts;
        private readonly int _receiveBufferSize;
        private readonly int _sendBufferSize;

        public Setting(int port
                        , int backLog, int maxConnections
                        , int receiveBufferSize
                        , int sendBufferSize
                        , int maxSimultaneousAceepts)
        {
            this._localEndPoint = new IPEndPoint(IPAddress.Any, port);
            this._backLog = backLog;
            this._maxConnections = maxConnections;
            this._receiveBufferSize = receiveBufferSize;
            this._sendBufferSize = sendBufferSize;
            this._maxSimultaneousAceepts = maxSimultaneousAceepts;
        }

        public Setting(int port
                        , IPEndPoint endPoint    
                        , int backLog, int maxConnections
                        , int receiveBufferSize
                        , int sendBufferSize
                        , int maxSimultaneousAceepts)
        {
            this._localEndPoint = endPoint;
            this._backLog = backLog;
            this._maxConnections = maxConnections;
            this._receiveBufferSize = receiveBufferSize;
            this._sendBufferSize = sendBufferSize;
            this._maxSimultaneousAceepts = maxSimultaneousAceepts;
        }      

        public IPEndPoint LocalEndPoint
        {
            get
            {
                return _localEndPoint;
            }
        }

        public int BackLog
        {
            get { return _backLog; }
        }

        public int MaxConnections
        {
            get { return _maxConnections; }
        }

        public int ReceiveBufferSize
        {
            get { return _receiveBufferSize; }
        }

        public int SendBufferSize
        {
            get { return _sendBufferSize; }
        }

        public int MaxSimulateneousAccepts
        {
            get { return _maxSimultaneousAceepts; }
        }

    }
}
