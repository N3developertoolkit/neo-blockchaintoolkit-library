using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore
    {
        class MemoryCache : ICache
        {
            readonly Func<UInt160, IEnumerable<(byte[] key, byte[] value)>> funcEnumStates;
            ImmutableDictionary<UInt160, (byte[] Key, byte[] Value)[]> memoryCache = ImmutableDictionary<UInt160, (byte[] Key, byte[] Value)[]>.Empty;

            public MemoryCache(Func<UInt160, IEnumerable<(byte[] key, byte[] value)>> funcEnumStates)
            {
                this.funcEnumStates = funcEnumStates;
            }

            public void Dispose()
            {
            }

            public IEnumerable<(byte[] key, byte[] value)> Seek(UInt160 contractHash, ReadOnlyMemory<byte> prefix, SeekDirection direction)
            {
                var comparer = direction == SeekDirection.Forward
                    ? ByteArrayComparer.Default
                    : ByteArrayComparer.Reverse;

                return GetCachedStates(contractHash)
                    .Where(kvp => prefix.Length == 0 || comparer.Compare(kvp.key, prefix.Span) >= 0)
                    .OrderBy(kvp => kvp.key, comparer);
            }

            public bool TryGet(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[] value)
            {
                var kvp = GetCachedStates(contractHash)
                    .SingleOrDefault(_kvp => key.Span.SequenceEqual(_kvp.key));

                if (kvp.value == default(byte[]))
                {
                    value = Array.Empty<byte>();
                    return false;
                }

                value = kvp.value;
                return true;
            }

            (byte[] key, byte[] value)[] GetCachedStates(UInt160 contractHash)
            {
                return ImmutableInterlocked.GetOrAdd(ref memoryCache, contractHash, key => funcEnumStates(key).ToArray());
            }
        }
    }
}
