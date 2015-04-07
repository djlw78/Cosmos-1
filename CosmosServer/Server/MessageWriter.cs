using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cosmos.Server
{
public static class MessageWriter
{
    internal delegate void MessageWriteToEventHandler(object sender, int sessionId, int handlerId, object message);
    internal static event MessageWriteToEventHandler OnWriteTo;

    internal delegate void MessageWriteToAllEventHandler(object sender, int handlerId, object message);
    internal static event MessageWriteToAllEventHandler OnWriteToAll;


    public static void WriteTo(int sessionId, int handlerId, object message)
    {
        OnWriteTo(null, sessionId, handlerId, message);
    }

    public static void WriteToAll(int handlerId, object message)
    {
        OnWriteToAll(null, handlerId, message);
    }
}
}
