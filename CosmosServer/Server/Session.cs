using System.Net.Sockets;

namespace Cosmos.Server
{
    public class Session
    {
        Socket _socket;
        int _handlerId;
        object _payload;

        public delegate void MessageWriteEventHandler(object sender, Socket socket, int handlerId, object message);
        public event MessageWriteEventHandler OnWrite;

        public delegate void MessageWriteToAllEventHandler(object sender, int ignoreSessionId, int handlerId, object message);
        public event MessageWriteToAllEventHandler OnWriteToAll;

        public delegate void MessageWriteToEventHandler(object sender, int sessionId, int handlerId, object message);
        public event MessageWriteToEventHandler OnWriteTo;

        public Session(Socket socket, int handlerId, object payload)
        {
            _socket = socket;
            _handlerId = handlerId;
            _payload = payload;
        }

        public int SessionId
        {
            get
            {
                return _socket.GetHashCode();
            }
        }

        public int HandlerId
        {
            get
            {
                return _handlerId;
            }
        }

        internal Socket Socket
        {
            get { return _socket; }
        }

        public object Payload
        {
            get { return _payload; }
        }

        public void Write(object message)
        {
            OnWrite(this, Socket, 0, message);
        }

        public void Write(int handlerId, object message)
        {
            OnWrite(this, Socket, 0, message);
        }

        public void WriteToAll(object message)
        {
            WriteToAll(SessionId, 0, message);
        }

        public void WriteToAll(int ignoreSessionId, object message)
        {
            WriteToAll(ignoreSessionId, 0, message);
        }

        public void WriteToAll(int ignoreSessionId, int handlerId, object message)
        {
            OnWriteToAll(this, ignoreSessionId, handlerId, message);
        }


        /// <summary>
        /// session id 에게 메세지를 보낸다.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="message"></param>
        public void WriteTo(int sessionId, object message)
        {
            OnWriteTo(this, sessionId, 0, message);
        }

        /// <summary>
        /// session id 에게 메세지를 보낸다.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="message"></param>
        public void WriteTo(int sessionId, int handlerId, object message)
        {
            OnWriteTo(this, sessionId, handlerId, message);
        }
    }
}
