using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cosmos.Server
{
public static class MessageWriter
{
    internal delegate void MessageWriteToEventHandler(int sessionId, byte[] payload);
    internal static event MessageWriteToEventHandler OnWriteTo;

    internal delegate void MessageWriteToAllEventHandler(byte[] payload);
    internal static event MessageWriteToAllEventHandler OnWriteToAll;

    public static void WriteTo(int sessionId, byte[] payload)
    {
        OnWriteTo(sessionId, payload);
    }

    public static void WriteToAll(byte[] payload)
    {
        OnWriteToAll(payload);
    }
}
}
