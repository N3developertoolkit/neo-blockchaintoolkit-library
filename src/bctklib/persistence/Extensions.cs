using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.Persistence;
using OneOf;
using RocksDbSharp;
using None = OneOf.Types.None;

namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableSortedDictionary<ReadOnlyMemory<byte>, OneOf<ReadOnlyMemory<byte>, None>>;

    static class Extensions
    {
        public static IEnumerable<(byte[] key, byte[] value)> Seek(this RocksDb db, ColumnFamilyHandle columnFamily, byte[]? key, SeekDirection direction, ReadOptions? readOptions)
        {
            key ??= Array.Empty<byte>();
            using var iterator = db.NewIterator(columnFamily, readOptions);

            Func<Iterator> iteratorNext;
            if (direction == SeekDirection.Forward)
            {
                iterator.Seek(key);
                iteratorNext = iterator.Next;
            }
            else
            {
                iterator.SeekForPrev(key);
                iteratorNext = iterator.Prev;
            }

            while (iterator.Valid())
            {
                yield return (iterator.Key(), iterator.Value());
                iteratorNext();
            }
        }

        public static ReadOnlyMemory<byte> CloneAsReadOnlyMemory(this byte[]? array)
        {
            return array == null ? default : array.AsSpan().ToArray();
        }
 
        public static byte[]? TryGet(this TrackingMap trackingMap, IReadOnlyStore store, byte[]? key)
        {
            if (trackingMap.TryGetValue(key ?? default, out var mapValue))
            {
                return mapValue.Match<byte[]?>(v => v.ToArray(), _ => null);
            }

            return store.TryGet(key);
        }

        public static IEnumerable<(byte[] Key, byte[] Value)> Seek(this TrackingMap trackingMap, IReadOnlyStore store, byte[]? key, SeekDirection direction)
        {
            key ??= Array.Empty<byte>();
            var comparer = direction == SeekDirection.Forward 
                ? ReadOnlyMemoryComparer.Default 
                : ReadOnlyMemoryComparer.Reverse;

            var memoryItems = trackingMap
                .Where(kvp => kvp.Value.IsT0)
                .Where(kvp => key.Length == 0 || comparer.Compare(kvp.Key, key) >= 0)
                .Select(kvp => (Key: kvp.Key.ToArray(), Value: kvp.Value.AsT0.ToArray()));

            var storeItems = store
                .Seek(key, direction)
                .Where<(byte[] Key, byte[] Value)>(kvp => !trackingMap.ContainsKey(kvp.Key));

            return memoryItems.Concat(storeItems).OrderBy(kvp => kvp.Key, comparer);
        }
    }
}