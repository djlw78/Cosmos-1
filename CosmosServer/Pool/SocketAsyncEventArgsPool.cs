using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace CosmosServer.Pool
{
    public sealed class SocketAsyncEventArgsPool
    {
        Stack<SocketAsyncEventArgs> pool;

        public SocketAsyncEventArgsPool(int capacity)
        {
            this.pool = new Stack<SocketAsyncEventArgs>(capacity);
        }

        public void Push(SocketAsyncEventArgs item)
        {
            if (item == null) { throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null"); }
            lock (pool)
            {
                pool.Push(item);
            }
        }

        public SocketAsyncEventArgs Pop()
        {
            lock (pool)
            {
                return pool.Pop();
            }
        }

        public int Count
        {
            get { return pool.Count; }
        }
    }
}
