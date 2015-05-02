using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;

namespace CosmosServer.Pool
{
    public sealed class SocketAsyncEventArgsPool
    {
        ConcurrentBag<SocketAsyncEventArgs> poolOfSAEA;

        public SocketAsyncEventArgsPool(int capacity)
        {
            poolOfSAEA = new ConcurrentBag<SocketAsyncEventArgs>();
        }

        public void Push(SocketAsyncEventArgs saea)
        {
            poolOfSAEA.Add(saea);
        }

        public bool Pop(out SocketAsyncEventArgs saea)
        {
            return poolOfSAEA.TryTake(out saea);
        }

        public int Count
        {
            get { return poolOfSAEA.Count;  }
        }
    }
}
