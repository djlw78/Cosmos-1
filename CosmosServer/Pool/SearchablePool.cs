using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cosmos.Pool
{
internal abstract class SearchablePool<K, V>
{
    private int numberOfObjects;
    private ConcurrentDictionary<K, V> objectMap;
    private ConcurrentBag<V> objectPool;

    internal SearchablePool(int numberOfObjects)
    {
        this.numberOfObjects = numberOfObjects;
        Trace.TraceInformation("PoolableService {0} is initializing...", this.GetType().Name.ToString());
        Trace.TraceInformation("Number of objects : {0}", numberOfObjects);

        objectPool = new ConcurrentBag<V>();
        objectMap = new ConcurrentDictionary<K, V>(Environment.ProcessorCount * 2, numberOfObjects);
    }

    protected void Initialize(Func<V> objectGenerator)
    {
        Parallel.For(0, numberOfObjects, (loopState) =>
        {
            V v = objectGenerator();
            objectPool.Add(v);
        });
    }

    /// <summary>
    /// Object pool 에서 객체를 하나 빌리고 Map에 추가해준다.
    /// </summary>
    /// <param name="k"></param>
    /// <param name="objectInitializer"></param>
    /// <param name="v"></param>
    /// <returns></returns>
    protected bool BorrowObject(K k, Func<V, V> objectInitializer, out V v)
    {
        V value;

        if (objectPool.TryTake(out value))
        {
            value = objectInitializer(value);

            if (!objectMap.TryAdd(k, value))
            {
                objectPool.Add(value);
                v = default(V);
                return false;
            }
        }
        else
        {
            v = default(V);
            return false;
        }

        v = value;
        return true;
    }

    internal abstract bool Borrow(K k, out V v);

    /// <summary>
    /// Object를 Reset 하고 Pool에 반납한다.
    /// </summary>
    /// <param name="k"></param>
    /// <param name="objectResetter"></param>
    /// <param name="v"></param>
    /// <returns></returns>
    protected bool ReturnObject(K k, Func<V, V> objectResetter)
    {
        V value;
        if (objectMap.TryRemove(k, out value))
        {
            value = objectResetter(value);
            objectPool.Add(value);
            return true;
        }
        else
        {
            return false;
        }
    }

    internal abstract bool Return(K k);

    internal bool GetBorrowingObject(K k, out V v)
    {
        return objectMap.TryGetValue(k, out v);
    }

    internal IEnumerator<V> GetBorrowingObjects()
    {
        return objectMap.Values.GetEnumerator();
    }
}
}
