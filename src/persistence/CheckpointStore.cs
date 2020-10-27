using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.IO.Caching;
using Neo.Persistence;
using OneOf;

namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableSortedDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    public partial class CheckpointStore : IStore
    {
        private readonly static OneOf.Types.None NONE_INSTANCE = new OneOf.Types.None();

        private readonly IReadOnlyStore store;
        private readonly IDisposable? checkpointCleanup;
        private readonly ConcurrentDictionary<byte, DataTracker> dataTrackers = new ConcurrentDictionary<byte, DataTracker>();

        public CheckpointStore(IReadOnlyStore store, IDisposable? checkpointCleanup = null)
        {
            this.store = store;
            this.checkpointCleanup = checkpointCleanup;
        }

        public void Dispose()
        {
            if (store is IDisposable disposable) disposable.Dispose();
            checkpointCleanup?.Dispose();
        }

        private DataTracker GetDataTracker(byte table)
            => dataTrackers.GetOrAdd(table, _ => new DataTracker(store, table));

        public byte[]? TryGet(byte table, byte[]? key)
            => GetDataTracker(table).TryGet(key);

        bool IReadOnlyStore.Contains(byte table, byte[] key)
            => null != GetDataTracker(table).TryGet(key);

        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte table, byte[]? prefix, SeekDirection direction)
            => GetDataTracker(table).Seek(prefix, direction);

        public void Put(byte table, byte[]? key, byte[] value)
            => GetDataTracker(table).Update(key, value);

        public void Delete(byte table, byte[]? key)
            => GetDataTracker(table).Update(key, NONE_INSTANCE);

        public ISnapshot GetSnapshot() => new Snapshot(this);

        static byte[]? TryGet(IReadOnlyStore store, byte table, byte[]? key, TrackingMap trackingMap)
        {
            if (trackingMap.TryGetValue(key ?? Array.Empty<byte>(), out var value))
            {
                return value.Match<byte[]?>(v => v, _ => null);
            }

            return store.TryGet(table, key);
        }

        private static IEnumerable<(byte[] Key, byte[] Value)> Seek(IReadOnlyStore store, byte table, byte[]? prefix, SeekDirection direction, TrackingMap trackingMap)
        {
            prefix ??= Array.Empty<byte>();
            var comparer = direction == SeekDirection.Forward ? ByteArrayComparer.Default : ByteArrayComparer.Reverse;

            var memoryItems = trackingMap
                .Where(kvp => kvp.Value.IsT0)
                .Where(kvp => prefix.Length == 0 || comparer.Compare(kvp.Key, prefix) >= 0)
                .Select(kvp => (kvp.Key, Value: kvp.Value.AsT0));

            var storeItems = store.Seek(table, prefix, direction)
                .Where(kvp => !trackingMap.ContainsKey(kvp.Key));

            return memoryItems.Concat(storeItems).OrderBy(kvp => kvp.Key, comparer);
            // if (prefix?.Length > 0)
            //     memoryItems = memoryItems.Where(kvp => );

            // memoryItems = memoryItems;

            // foreach (var kvp in memoryItems)
            // {
            //     yield return (kvp.Key, kvp.Value.AsT0);
            // }
                // .Where(kvp => (prefix.Length == 0) || comparer.Compare(kvp.Key, prefix) >= 0)


        }
    }
}
