using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Neo.Persistence;
using OneOf;

namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableSortedDictionary<byte[], OneOf<byte[]?, OneOf.Types.None>>;

    public partial class CheckpointStorageProvider : IDisposableStorageProvider
    {
        public readonly static TrackingMap EMPTY_TRACKING_MAP = TrackingMap.Empty.WithComparers(ByteArrayComparer.Default);

        readonly RocksDbStorageProvider? rocksDbStorageProvider;
        readonly IDisposable? checkpointCleanup;
        ImmutableDictionary<string, TrackingMap> trackingMaps = ImmutableDictionary<string, TrackingMap>.Empty;

        public CheckpointStorageProvider(RocksDbStorageProvider? rocksDbStorageProvider, IDisposable? checkpointCleanup = null)
        {
            this.rocksDbStorageProvider = rocksDbStorageProvider;
            this.checkpointCleanup = checkpointCleanup;
        }

        public void Dispose()
        {
            rocksDbStorageProvider?.Dispose();
            checkpointCleanup?.Dispose();
        }

        internal TrackingMap GetTrackingMap(string storeName) => trackingMaps.TryGetValue(storeName, out var map) ? map : EMPTY_TRACKING_MAP;

        IReadOnlyStore GetReadOnlyStore(string storeName)
            => (rocksDbStorageProvider != null && rocksDbStorageProvider.TryGetStore(storeName, out var _store))
                ? _store
                : NullStore.Instance;

        public IStore GetStore(string storeName)
        {
            return new Store(this, storeName);
        }

        internal ISnapshot GetSnapshot(string storeName)
        {
            var map = GetTrackingMap(storeName);
            return new CheckpointStorageProvider.Snapshot(this, map, storeName);
        }

        internal byte[]? TryGet(string storeName, byte[]? key) => TryGet(storeName, GetTrackingMap(storeName), key);

        internal byte[]? TryGet(string storeName, TrackingMap map, byte[]? key)
        {
            if (map.TryGetValue(key ?? Array.Empty<byte>(), out var mapValue))
            {
                return mapValue.Match<byte[]?>(v => v, n => null);
            }

            return GetReadOnlyStore(storeName).TryGet(key);
        }

        internal IEnumerable<(byte[] Key, byte[]? Value)> Seek(string storeName, byte[]? key, SeekDirection direction)
            => Seek(storeName, GetTrackingMap(storeName), key, direction);

        internal IEnumerable<(byte[] Key, byte[]? Value)> Seek(string storeName, TrackingMap map, byte[]? key, SeekDirection direction)
        {
            key ??= Array.Empty<byte>();
            var comparer = direction == SeekDirection.Forward ? ByteArrayComparer.Default : ByteArrayComparer.Reverse;

            var memoryItems = map
                .Where(kvp => kvp.Value.IsT0)
                .Where(kvp => key.Length == 0 || comparer.Compare(kvp.Key, key) >= 0)
                .Select(kvp => (kvp.Key, Value: kvp.Value.AsT0));

            var storeItems = GetReadOnlyStore(storeName)
                .Seek(key, direction)
                .Where<(byte[] Key, byte[]? Value)>(kvp => !map.ContainsKey(kvp.Key));

            return memoryItems.Concat(storeItems).OrderBy(kvp => kvp.Key, comparer);
        }

        internal void Update(string storeName, byte[]? key, OneOf<byte[]?, OneOf.Types.None> value)
        {
            var trackingMap = trackingMaps.TryGetValue(storeName, out var map) ? map : EMPTY_TRACKING_MAP;
            trackingMap = trackingMap.SetItem(key ?? Array.Empty<byte>(), value);
            trackingMaps = trackingMaps.SetItem(storeName, trackingMap);
        }

        internal void Update(string storeName, TrackingMap changes)
        {
            var trackingMap = trackingMaps.TryGetValue(storeName, out var map) ? map : EMPTY_TRACKING_MAP;
            foreach (var change in changes)
            {
                trackingMap.SetItem(change.Key, change.Value);
            }
            trackingMaps = trackingMaps.SetItem(storeName, trackingMap);
        }
    }
}