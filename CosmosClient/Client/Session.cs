using System.Net.Sockets;

namespace Cosmos.Client
{
    public class Session
    {
        readonly int _handlerId;
        readonly object _payload;

        public Session(int handlerId, object payload)
        {
            _handlerId = handlerId;
            _payload = payload;
        }

        public int HandlerId
        {
            get
            {
                return _handlerId;
            }
        }

        public object Payload
        {
            get { return _payload; }
        }       
    }
}
