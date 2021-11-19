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
    using TrackingMap = ImmutableDictionary<ReadOnlyMemory<byte>, OneOf<ReadOnlyMemory<byte>, None>>;

    static class Extensions
    {
        readonly static ColumnFamilyOptions defaultColumnFamilyOptions = new ColumnFamilyOptions();
        public static ColumnFamilyHandle GetOrCreateColumnFamily(this RocksDb db, string familyName, ColumnFamilyOptions? options = null)
        {
            if (!db.TryGetColumnFamily(familyName, out var familyHandle))
            {
                familyHandle = db.CreateColumnFamily(options ?? defaultColumnFamilyOptions, familyName);
            }
            return familyHandle;
        }

        public static IEnumerable<(byte[] key, byte[] value)> Seek(this RocksDb db, ColumnFamilyHandle columnFamily, ReadOnlySpan<byte> prefix, SeekDirection direction, ReadOptions? readOptions)
        {
            // Note, behavior of IReadOnlyStore.Seek method is inconsistent when key is empty and SeekDirection is backwards.
            // MemoryStore returns all items in reverse order while LevelDbStore and RocksDbStore return an empty enumerable.
            // This inconsistency is tracked by https://github.com/neo-project/neo/issues/2634.
            // Luckily, the combination of empty key + backwards seek isn't used anywhere in the neo code base as of v3.0.3
            // For now, explicitly throw in this situation rather than choosing one inconsistent behavior over the other

            if (prefix.Length == 0 && direction == SeekDirection.Backward)
            {
                throw new InvalidOperationException("https://github.com/neo-project/neo/issues/2634");
            }

            var iterator = db.NewIterator(columnFamily, readOptions);

            if (direction == SeekDirection.Forward)
            {
                Seek(iterator, prefix);
                return SeekInternal(iterator, iterator.Next);
            }
            else
            {
                SeekForPrev(iterator, prefix);
                return SeekInternal(iterator, iterator.Prev);
            }

            IEnumerable<(byte[] key, byte[] value)> SeekInternal(Iterator iterator, Func<Iterator> nextAction)
            {
                using (iterator)
                {
                    while (iterator.Valid())
                    {
                        yield return (iterator.Key(), iterator.Value());
                        nextAction();
                    }
                }
            }

            unsafe static Iterator Seek(Iterator @this, ReadOnlySpan<byte> prefix)
            {
                fixed (byte* prefixPtr = prefix)
                {
                    return @this.Seek(prefixPtr, (ulong)prefix.Length);
                }
            }

            unsafe static Iterator SeekForPrev(Iterator @this, ReadOnlySpan<byte> prefix)
            {
                fixed (byte* prefixPtr = prefix)
                {
                    return @this.SeekForPrev(prefixPtr, (ulong)prefix.Length);
                }
            }
        }

        public static byte[]? TryGet(this TrackingMap trackingMap, IReadOnlyStore store, byte[]? key)
        {
            key ??= Array.Empty<byte>();
            if (trackingMap.TryGetValue(key, out var mapValue))
            {
                return mapValue.Match<byte[]?>(v => v.ToArray(), _ => null);
            }

            return store.TryGet(key);
        }

        public static IEnumerable<(byte[] Key, byte[] Value)> Seek(this TrackingMap trackingMap, IReadOnlyStore store, byte[]? key, SeekDirection direction)
        {
            key ??= Array.Empty<byte>();

            // See note above in Seek(RocksDb, ColumnFamilyHandle, ReadOnlySpan<byte>, SeekDirection, ReadOptions?)
            // regarding this InvalidOperationException 

            if (key.Length == 0 && direction == SeekDirection.Backward)
            {
                throw new InvalidOperationException("https://github.com/neo-project/neo/issues/2634");
            }

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