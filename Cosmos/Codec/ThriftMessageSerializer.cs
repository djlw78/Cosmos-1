using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Thrift.Protocol;
using Thrift.Transport;

namespace Cosmos.Codec
{
    public class ThriftMessageSerializer : IMessageSerializer
    {
        readonly static int _messageHeaderSize = 8;

        public int GetHeaderSize()
        {
            return _messageHeaderSize;
        }

        public static byte[] Serialize<T>(int handlerId, T message) where T : Thrift.Protocol.TBase
        {
            MemoryStream stream = new MemoryStream();
            TProtocol protocol = new TBinaryProtocol(new TStreamTransport(stream, stream));
            message.Write(protocol);
            byte[] payload = stream.ToArray();

            byte[] length = BitConverter.GetBytes(payload.Length);
            byte[] handler = BitConverter.GetBytes(handlerId);

            byte[] totalMessage = new byte[_messageHeaderSize + payload.Length];

            System.Buffer.BlockCopy(length, 0, totalMessage, 0, 4);
            System.Buffer.BlockCopy(handler, 0, totalMessage, 4, 4);
            System.Buffer.BlockCopy(payload, 0, totalMessage, _messageHeaderSize, payload.Length);

            return totalMessage;
        }

        public bool CheckHaderValidation(byte[] _header, int offset, out int _totalDataLength, out int _handlerId)
        {
            try
            {
                // 메세지 헤더인 경우 헤더를 해석해서 BodySize와 HandlerId를 얻는다.
                _totalDataLength = BitConverter.ToInt32(_header, offset);
                _handlerId = BitConverter.ToInt32(_header, offset + 4);

                // 데이터 길이가 1보다 작거나(0포함) Int16.MaxValue 보다 큰 경우는 valid 하지 않다고 판단.
                if (_totalDataLength < 1 && _totalDataLength > Int16.MaxValue)
                {
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static T Deserialize<T>(byte[] payload) where T : Thrift.Protocol.TBase, new()
        {
            MemoryStream stream = new MemoryStream(payload);
            TProtocol protocol = new TBinaryProtocol(new TStreamTransport(stream, stream));
            T t = new T();
            t.Read(protocol);
            return t;
        }

    }
}
