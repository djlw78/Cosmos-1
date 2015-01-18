using System.Net.Sockets;

namespace Cosmos.Server
{
    public class Session
    {
        SocketAsyncEventArgs _saeaWrite;
        int _handlerId;
        object _payload;

        public delegate void MessageWriteEventHandler(object sender, SocketAsyncEventArgs saeaWrite, int handlerId, object message);
        public event MessageWriteEventHandler OnWrite;

        public delegate void MessageWriteToAllEventHandler(object sender, int ignoreSessionId, int handlerId, object message);
        public event MessageWriteToAllEventHandler OnWriteToAll;

        public delegate void MessageWriteToEventHandler(object sender, int sessionId, int handlerId, object message);
        public event MessageWriteToEventHandler OnWriteTo;

        public Session(SocketAsyncEventArgs saeaWrite, int handlerId, object payload)
        {
            _saeaWrite = saeaWrite;
            _handlerId = handlerId;
            _payload = payload;
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


        public object Payload
        {
            get { return _payload; }
        }

        public void Write(object message)
        {
            OnWrite(this, _saeaWrite, 0, message);
        }

        public void Write(int handlerId, object message)
        {
            OnWrite(this, _saeaWrite, 0, message);
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
