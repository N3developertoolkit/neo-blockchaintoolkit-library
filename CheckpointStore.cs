using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.IO.Caching;
using Neo.Persistence;
using OneOf;

namespace Neo.Seattle.Persistence
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

        byte[]? IReadOnlyStore.TryGet(byte table, byte[]? key)
            => GetDataTracker(table).TryGet(key);

        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte table, byte[]? prefix, SeekDirection direction)
            => GetDataTracker(table).Seek(prefix, direction);

        void IStore.Put(byte table, byte[]? key, byte[] value) 
            => GetDataTracker(table).Update(key, value);

        void IStore.Delete(byte table, byte[]? key)
            => GetDataTracker(table).Update(key, NONE_INSTANCE);

        ISnapshot IStore.GetSnapshot() => new Snapshot(this);

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
            var memoryItems = trackingMap
                .Where(kvp => kvp.Key.AsSpan().StartsWith(prefix ?? Array.Empty<byte>()) && kvp.Value.IsT0)
                .Select(kvp => (key: kvp.Key, value: kvp.Value.AsT0));

            IEnumerable<(byte[] key, byte[] value)> allItems = store.Seek(table, prefix, SeekDirection.Forward)
                .Where(kvp => !trackingMap.ContainsKey(kvp.Key))
                .Concat(memoryItems);

            return direction == SeekDirection.Forward
                ? allItems.OrderBy(t => t.key, ByteArrayComparer.Default)
                : allItems.OrderByDescending(t => t.key, ByteArrayComparer.Default);
        }
    }
}
