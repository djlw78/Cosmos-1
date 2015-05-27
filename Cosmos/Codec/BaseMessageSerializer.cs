using System;
using System.Collections.Generic;
using System.Text;

namespace Cosmos.Codec
{
    namespace Cosmos.Codec
    {
        public abstract class BaseMessageSerializer
        {
            public const int _MESSAGE_HEADER_SIZE = sizeof(UInt32) + sizeof(UInt16);

            protected byte[] Serialize(ushort handlerId, byte[] payload)
            {
                byte[] length = BitConverter.GetBytes(payload.Length);
                byte[] handler = BitConverter.GetBytes(handlerId);

                byte[] totalMessage = new byte[_MESSAGE_HEADER_SIZE + payload.Length];

                System.Buffer.BlockCopy(length, 0, totalMessage, 0, sizeof(UInt32));
                System.Buffer.BlockCopy(handler, 0, totalMessage, sizeof(UInt32), sizeof(UInt16));
                System.Buffer.BlockCopy(payload, 0, totalMessage, _MESSAGE_HEADER_SIZE, payload.Length);

                return totalMessage;
            }

            /// <summary>
            /// 헤더 사이즈가 맞는지 검증한다.
            /// </summary>
            /// <param name="_header"></param>
            /// <param name="offset"></param>
            /// <param name="_totalDataLength"></param>
            /// <param name="_handlerId"></param>
            /// <returns></returns>
            public bool CheckHaderValidation(byte[] _header, int offset, out int _totalDataLength, out ushort _handlerId)
            {
                try
                {
                    // 메세지 헤더인 경우 헤더를 해석해서 BodySize와 HandlerId를 얻는다.
                    _totalDataLength = BitConverter.ToInt32(_header, offset);
                    _handlerId = BitConverter.ToUInt16(_header, offset + sizeof(int));

                    // 데이터 길이가 1보다 작거나(0포함) Int16.MaxValue 보다 큰 경우는 valid 하지 않다고 판단.
                    if (_totalDataLength < 1 && _totalDataLength > UInt16.MaxValue)
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
            public abstract byte[] Serialize<T>(ushort handlerId, T message);
        }
    }
}