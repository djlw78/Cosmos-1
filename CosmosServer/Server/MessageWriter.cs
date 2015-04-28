using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cosmos.Server
{
public static class MessageWriter
{
    internal delegate void MessageWriteToEventHandler(int sessionId, ushort handlerId, object message);
    internal static event MessageWriteToEventHandler OnWriteTo;

    internal delegate void MessageWriteToAllEventHandler(ushort handlerId, object message);
    internal static event MessageWriteToAllEventHandler OnWriteToAll;

    public static void WriteTo<T>(int sessionId, ushort handlerId, T message)
    {
        OnWriteTo(sessionId, handlerId, message);
    }

    public static void WriteToAll<T>(ushort handlerId, T message)
    {
        OnWriteToAll(handlerId, message);
    }
}
}
