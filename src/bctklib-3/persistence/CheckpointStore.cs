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

    public partial class CheckpointStore : IStore, IExpressStore
    {
        readonly static OneOf.Types.None NONE_INSTANCE = new OneOf.Types.None();
        readonly static TrackingMap EMPTY_TRACKING_MAP = TrackingMap.Empty.WithComparers(ByteArrayComparer.Default);

        readonly IExpressReadOnlyStore store;
        readonly bool disposeStore;
        readonly IDisposable? checkpointCleanup;
        ImmutableDictionary<byte, TrackingMap> trackingMaps = ImmutableDictionary<byte, TrackingMap>.Empty;

        public CheckpointStore(IExpressReadOnlyStore store) : this(store, true, null)
        {
        }

        public CheckpointStore(IExpressReadOnlyStore store, bool disposeStore) : this(store, disposeStore, null)
        {
        }

        public CheckpointStore(IExpressReadOnlyStore store, IDisposable? checkpointCleanup) : this(store, true, checkpointCleanup)
        {
        }

        public CheckpointStore(IExpressReadOnlyStore store, bool disposeStore, IDisposable? checkpointCleanup)
        {
            this.store = store;
            this.disposeStore = disposeStore;
            this.checkpointCleanup = checkpointCleanup;
        }

        public void Dispose()
        {
            if (disposeStore && store is IDisposable disposable) disposable.Dispose();
            checkpointCleanup?.Dispose();
        }

        public byte[]? TryGet(byte table, byte[]? key)
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

        public bool Contains(byte table, byte[]? key)
        {
            var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
            return Contains(trackingMap, table, key);
        }

        bool Contains(TrackingMap trackingMap, byte table, byte[]? key)
        {
            if (trackingMap.TryGetValue(key ?? Array.Empty<byte>(), out var mapValue))
            {
                return mapValue.Match(v => true, n => false);
            }

            return store.Contains(table, key);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[]? prefix, SeekDirection direction)
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

        public void Put(byte table, byte[]? key, byte[] value)
        {
            Update(table, key, value);
        }

        public void PutSync(byte table, byte[]? key, byte[] value)
        {
            Update(table, key, value);
        }

        public void Delete(byte table, byte[]? key)
        {
            Update(table, key, NONE_INSTANCE);
        }

        void Update(byte table, byte[]? key, OneOf<byte[], OneOf.Types.None> value)
        {
            var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
            trackingMap = trackingMap.SetItem(key ?? Array.Empty<byte>(), value);
            trackingMaps = trackingMaps.SetItem(table, trackingMap);
        }

        byte[]? IReadOnlyStore.TryGet(byte[]? key) => TryGet(default, key);
        bool IReadOnlyStore.Contains(byte[]? key) => Contains(default, key);
        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte[]? key, SeekDirection direction) => Seek(default, key, direction);
        void IStore.Put(byte[]? key, byte[] value) => Put(default, key, value);
        void IStore.PutSync(byte[]? key, byte[] value) => PutSync(default, key, value);
        void IStore.Delete(byte[]? key) => Delete(default, key);
    }
}