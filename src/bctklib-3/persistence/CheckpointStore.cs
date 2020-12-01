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
        readonly static OneOf.Types.None NONE_INSTANCE = new OneOf.Types.None();
        readonly static TrackingMap EMPTY_TRACKING_MAP = TrackingMap.Empty.WithComparers(ByteArrayComparer.Default);

        readonly IReadOnlyStore store;
        readonly IDisposable? checkpointCleanup;
        ImmutableDictionary<byte, TrackingMap> trackingMaps = ImmutableDictionary<byte, TrackingMap>.Empty;

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

        byte[]? IReadOnlyStore.TryGet(byte table, byte[]? key)
        {
            var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
            return TryGet(trackingMap, table, key);
        }

        byte[]? TryGet(TrackingMap trackingMap, byte table, byte[]? key)
        {
            if (trackingMap.TryGetValue(key ?? Array.Empty<byte>(), out var mapValue))
            {
                return mapValue.Match<byte[]?>(v => v, n => null);
            }

            return store.TryGet(table, key);
        }

        bool IReadOnlyStore.Contains(byte table, byte[] key)
        {
            var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
            return Contains(trackingMap, table, key);
        }

        bool Contains(TrackingMap trackingMap, byte table, byte[] key)
        {
            if (trackingMap.TryGetValue(key ?? Array.Empty<byte>(), out var mapValue))
            {
                return mapValue.Match(v => true, n => false);
            }

            return store.Contains(table, key);
        }


        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte table, byte[]? prefix, SeekDirection direction)
        {
            var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
            return Seek(trackingMap, table, prefix, direction);
        }

        IEnumerable<(byte[] Key, byte[] Value)> Seek(TrackingMap trackingMap, byte table, byte[]? prefix, SeekDirection direction)
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
        }

        ISnapshot IStore.GetSnapshot() => new Snapshot(this);

        void Put(byte table, byte[]? key, OneOf<byte[], OneOf.Types.None> value)
        {
            var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
            trackingMap = trackingMap.SetItem(key ?? Array.Empty<byte>(), value);
            trackingMaps = trackingMaps.SetItem(table, trackingMap);
        }

        void IStore.Put(byte table, byte[]? key, byte[] value)
        {
            Put(table, key, value);
        }

        void IStore.Delete(byte table, byte[]? key)
        {
            Put(table, key, NONE_INSTANCE);
        }
    }
}
