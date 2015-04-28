using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Cosmos.Codec
{
public class ClassMessageSerializer : BaseMessageSerializer
{
    public override byte[] Serialize<T>(ushort handlerId, T message)
    {
        try
        {
            MemoryStream ms = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(ms, message);

            byte[] payload = ms.ToArray();
            return base.Serialize(handlerId, payload);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public T Deserialize<T>(byte[] data)
    {
        try
        {
            MemoryStream ms = new MemoryStream();
            ms.Write(data, 0, data.Length);
            ms.Position = 0;
            BinaryFormatter formatter = new BinaryFormatter();
            return (T) formatter.Deserialize(ms);
        }
        catch (Exception)
        {
            throw;
        }
    }
}
}
