using System;
using System.Collections.Generic;
using System.Text;

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
    /// 헤더 정보를 out param으로 받는다. header 유효성을 검증한다
    /// </summary>
    /// <param name="_header"></param>
    /// <param name="_totalDataLength"></param>
    /// <param name="_handlerId"></param>
    bool CheckHaderValidation(byte[] _header, int offset, out int _totalDataLength, out ushort _handlerId);

    byte[] Serialize<T>(ushort handlerId, T message);
}
}
