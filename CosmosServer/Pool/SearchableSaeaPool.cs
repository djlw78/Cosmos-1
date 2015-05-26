using Cosmos.Buffer;
using Cosmos.Codec;
using Cosmos.Server;
using Cosmos.Token;
using System;
using System.Net.Sockets;

namespace Cosmos.Pool
{
internal class SearchableSaeaPool : SearchablePool<int, SocketAsyncEventArgs>
{
    internal enum TokenType
    {
        READ,
        WRITE
    }

    internal SearchableSaeaPool(int numberOfObjects, Setting setting, BufferManager bufferManager, EventHandler<SocketAsyncEventArgs> eventHandler, TokenType tokenType)
    : base(numberOfObjects)
    {
        base.Initialize(() =>
        {
            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
            bufferManager.SetBuffer(saea);
            saea.Completed += eventHandler;
            if (tokenType == TokenType.READ)
            {
                saea.UserToken = new ReadToken(saea, setting.ReceiveBufferSize, BaseMessageSerializer._MESSAGE_HEADER_SIZE);
            }
            else if (tokenType == TokenType.WRITE)
            {
                saea.UserToken = new WriteToken(saea, setting.SendBufferSize);
            }
            return saea;
        });
    }

    internal override bool Borrow(int key, out SocketAsyncEventArgs saea)
    {
        return base.BorrowObject(key, (v) =>
        {
            return v;
        }, out saea);
    }

    internal override bool Return(int key)
    {
        return base.ReturnObject(key, (v) =>
        {
            return v;
        });
    }

}
}
