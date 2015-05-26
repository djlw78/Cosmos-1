using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cosmos.Pool
{
internal abstract class DefaultPool<T>
{
    private int _numberOfObjects;
    private ConcurrentBag<T> objectPool;

    internal DefaultPool(int numberOfObjects)
    {
        _numberOfObjects = numberOfObjects;
        objectPool = new ConcurrentBag<T>();
    }

    protected void Initialize(Func<T> objectGenerator)
    {
        Parallel.For(0, _numberOfObjects, (loopState) =>
        {
            T t = objectGenerator();
            objectPool.Add(t);
        });
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="objectInitializer">Define how object need to be initialized while borrowing.</param>
    /// <param name="t"></param>
    /// <returns></returns>
    protected bool BorrowObject(Func<T, T> objectInitializer, out T t)
    {
        T typeObject;

        if (objectPool.TryTake(out typeObject))
        {
            typeObject = objectInitializer(typeObject);
            t = typeObject;
            return true;
        }
        else
        {
            t = default(T);
            return false;
        }
    }

    protected void ReturnObject(Func<T, T> objectResetter, T returningObject)
    {
        T resettedObject = objectResetter(returningObject);
        objectPool.Add(resettedObject);
    }

    internal abstract bool Borrow(out T t);
    internal abstract void Return(T t);
}
}