using System;
using System.Collections.Generic;
using System.Text;
using Thrift.Protocol;

namespace Cosmos.Codec
{
    public interface IMessageSerializer
    {
        /// <summary>
        /// MessageHeader 사이즈를 리턴한다.
        /// </summary>
        /// <returns></returns>
        int GetHeaderSize();

        /// <summary>
        /// object를 byte 배열로 serialization 한다.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        //byte[] Serialize<T>(int handlerId, T message) where T : TBase;

        /// <summary>
        /// 헤더 정보를 out param으로 받는다. header 유효성을 검증한다
        /// </summary>
        /// <param name="_header"></param>
        /// <param name="_totalDataLength"></param>
        /// <param name="_handlerId"></param>
        bool CheckHaderValidation(byte[] _header, int offset, out int _totalDataLength, out int _handlerId);

        /// <summary>
        /// data를 object를 deserialization 한다.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        //T Deserialize<T>(byte[] data) where T : TBase, new();
    }
}
