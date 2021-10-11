using System;
using System.Collections.Generic;
using System.Threading;

namespace Neo.BlockchainToolkit.Persistence
{
    class LockingDictionary<TKey, TValue> where TKey : notnull
    {
        readonly Dictionary<TKey, TValue> cache = new();
        readonly ReaderWriterLockSlim cacheLock = new();

        public TValue GetOrAdd(in TKey key, Func<TKey, TValue> factory)
        {
            cacheLock.EnterUpgradeableReadLock();
            try
            {
                if (cache.TryGetValue(key, out var value)) return value;

                value = factory(key);
                cacheLock.EnterWriteLock();
                try
                {
                    if (cache.TryGetValue(key, out var _value))
                    {
                        value = _value;
                    }
                    else
                    {
                        cache.Add(key, value);
                    }
                    return value;
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }
            }
            finally
            {
                cacheLock.ExitUpgradeableReadLock();
            }
        }
    }
}
