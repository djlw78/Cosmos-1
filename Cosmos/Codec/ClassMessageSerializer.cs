using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Cosmos.Codec
{
    public class ClassMessageSerializer : IMessageSerializer
    {
        readonly int _messageHeaderSize = 8;
        public int GetHeaderSize()
        {
            return _messageHeaderSize;
        }

        public byte[] Serialize(int handlerId, object message)
        {
            try
            {
                MemoryStream ms = new MemoryStream();
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, message);

                byte[] payload = ms.ToArray();

                byte[] length = BitConverter.GetBytes(payload.Length);
                byte[] handler = BitConverter.GetBytes(handlerId);

                byte[] totalMessage = new byte[_messageHeaderSize + payload.Length];

                System.Buffer.BlockCopy(length, 0, totalMessage, 0, 4);
                System.Buffer.BlockCopy(handler, 0, totalMessage, 4, 4);
                System.Buffer.BlockCopy(payload, 0, totalMessage, _messageHeaderSize, payload.Length);

                return totalMessage;
            }
            catch (Exception)
            {
                throw;
            }
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

        public object Deserialize(byte[] data)
        {
            try
            {
                MemoryStream ms = new MemoryStream();
                ms.Write(data, 0, data.Length);
                ms.Position = 0;
                BinaryFormatter formatter = new BinaryFormatter();
                object message = (object)formatter.Deserialize(ms);
                return message;
            }
            catch (Exception)
            {
                throw;
            }
        }        
    }
}
