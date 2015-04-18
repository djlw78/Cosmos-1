using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thrift.Protocol;

namespace Cosmos.Server
{
public static class MessageWriter
{
    public delegate void MessageWriteToEventHandler(int sessionId, int handlerId, TBase message);
    public static event MessageWriteToEventHandler OnWriteTo;

    public delegate void MessageWriteToAllEventHandler(int handlerId, TBase message);
    public static event MessageWriteToAllEventHandler OnWriteToAll;


    public static void WriteTo(int sessionId, int handlerId, TBase message)
    {
        OnWriteTo(sessionId, handlerId, message);
    }

    public static void WriteToAll(int handlerId, TBase message)
    {
        OnWriteToAll(handlerId, message);
    }
}
}
