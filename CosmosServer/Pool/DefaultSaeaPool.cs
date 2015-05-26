using Cosmos.Server;
using System;
using System.Net.Sockets;

namespace Cosmos.Pool
{
internal class DefaultSaeaPool : DefaultPool<SocketAsyncEventArgs>
{
    internal DefaultSaeaPool(int numberOfObjects, Setting setting, EventHandler<SocketAsyncEventArgs> eventHandler)
    : base(numberOfObjects)
    {
        base.Initialize(() =>
        {
            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
            saea.Completed += eventHandler;
            return saea;
        });
    }

    internal override bool Borrow(out SocketAsyncEventArgs socketAsyncEventArgs)
    {
        return base.BorrowObject((saea) =>
        {
            return saea;
        }, out socketAsyncEventArgs);
    }

    internal override void Return(SocketAsyncEventArgs returningSocketAsyncEventArgs)
    {
        base.ReturnObject((saea) =>
        {
            return saea;
        }, returningSocketAsyncEventArgs);
    }
}
}
