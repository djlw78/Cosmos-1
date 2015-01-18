using System.Collections.Generic;
using System.Net.Sockets;

namespace Cosmos.Buffer
{
    /// <summary>
    /// 큰 하나의 byte array를 만들고 Socket 의 IO operation이 그 공간을 사용할 수 있도록 offset을 지정해준다.
    /// </summary>
    public class BufferManager
    {
        int _totalBytesInBufferBlock;       // the total number of bytes controlled by the buffer pool        
        byte[] _hugeBufferBlock;            // byte array maintained by the Buffer Manager.
        Stack<int> _freeIndexPool;          // 해제 된 후 Stack으로 반환 된 버퍼 인덱스를 보관한다. (서버에서는 크게 쓸 일이 없음)
        int _currentIndex;                  // byte array index 지정을 위해 쓰이는 변수
        int _totalBufferSize;               // 하나의 byte array index에 할당할 buffer 크기를 지정한다.

        public BufferManager(int bufferSize, int totalBucketCount, int messageHeaderSize)
        {
            // 필요한 전체 버퍼 블록 사이즈를 계산한다. [ReceiveHeader][ReceivePayload][SendMessage] 총 3 영역
            this._totalBufferSize = messageHeaderSize + bufferSize;
            this._totalBytesInBufferBlock = _totalBufferSize * totalBucketCount;
            this._currentIndex = 0;
            this._freeIndexPool = new Stack<int>(totalBucketCount);
        }

        /// <summary>
        /// Allocates buffer space used by the buffer pool
        /// </summary>
        public void InitBuffer()
        {
            // Create one large buffer block.
            this._hugeBufferBlock = new byte[_totalBytesInBufferBlock];
        }

        // Divide that one large buffer block out to each SocketAsyncEventArg object.
        // Assign a buffer space from the buffer block to the 
        // specified SocketAsyncEventArgs object.
        //
        // returns true if the buffer was successfully set, else false
        public bool SetBuffer(SocketAsyncEventArgs args)
        {
            if (this._freeIndexPool.Count > 0)
            {
                //This if-statement is only true if you have called the FreeBuffer
                //method previously, which would put an offset for a buffer space 
                //back into this stack.
                args.SetBuffer(this._hugeBufferBlock, this._freeIndexPool.Pop(), this._totalBufferSize);
            }
            else
            {
                //Inside this else-statement is the code that is used to set the 
                //buffer for each SAEA object when the pool of SAEA objects is built
                //in the Init method.
                if ((this._totalBytesInBufferBlock - this._totalBufferSize) < this._currentIndex)
                {
                    return false;
                }
                args.SetBuffer(this._hugeBufferBlock, this._currentIndex, this._totalBufferSize);
                this._currentIndex += this._totalBufferSize;
            }
            return true;
        }

        // Removes the buffer from a SocketAsyncEventArg object.   This frees the
        // buffer back to the buffer pool. Try NOT to use the FreeBuffer method,
        // unless you need to destroy the SAEA object, or maybe in the case
        // of some exception handling. Instead, on the server
        // keep the same buffer space assigned to one SAEA object for the duration of
        // this app's running.
        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            this._freeIndexPool.Push(args.Offset);
            args.SetBuffer(null, 0, 0);
        }
    }
}
