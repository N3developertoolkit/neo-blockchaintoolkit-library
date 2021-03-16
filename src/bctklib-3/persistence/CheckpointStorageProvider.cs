using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Neo.Persistence;
using OneOf;
using None = OneOf.Types.None;

namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableSortedDictionary<byte[], OneOf<byte[]?, None>>;

    public partial class CheckpointStorageProvider : IDisposableStorageProvider
    {
        public readonly static TrackingMap EMPTY_TRACKING_MAP = TrackingMap.Empty.WithComparers(ByteArrayComparer.Default);

        readonly RocksDbStorageProvider? rocksDbStorageProvider;
        readonly IDisposable? checkpointCleanup;
        ImmutableDictionary<string, TrackingMap> trackingMaps = ImmutableDictionary<string, TrackingMap>.Empty;
        TrackingMap defaultTrackingMap = EMPTY_TRACKING_MAP;

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

        internal TrackingMap GetTrackingMap(string? storeName) 
            => storeName == null
                ? defaultTrackingMap 
                : trackingMaps.TryGetValue(storeName, out var trackingMap) 
                    ? trackingMap 
                    : EMPTY_TRACKING_MAP;

        IReadOnlyStore GetReadOnlyStore(string? storeName)
            => (rocksDbStorageProvider != null && rocksDbStorageProvider.TryGetStore(storeName, out var store))
                ? store
                : NullStore.Instance;

        public IStore GetStore(string? storeName)
        {
            return new Store(this, storeName);
        }

        internal ISnapshot GetSnapshot(string? storeName)
        {
            var map = GetTrackingMap(storeName);
            return new CheckpointStorageProvider.Snapshot(this, map, storeName);
        }

        internal byte[]? TryGet(string? storeName, byte[]? key) => TryGet(storeName, GetTrackingMap(storeName), key);

        internal byte[]? TryGet(string? storeName, TrackingMap map, byte[]? key)
        {
            if (map.TryGetValue(key ?? Array.Empty<byte>(), out var mapValue))
            {
                return mapValue.Match<byte[]?>(v => v, n => null);
            }

            return GetReadOnlyStore(storeName).TryGet(key);
        }

        internal IEnumerable<(byte[] Key, byte[]? Value)> Seek(string? storeName, byte[]? key, SeekDirection direction)
            => Seek(storeName, GetTrackingMap(storeName), key, direction);

        internal IEnumerable<(byte[] Key, byte[]? Value)> Seek(string? storeName, TrackingMap map, byte[]? key, SeekDirection direction)
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

        internal void Update(string? storeName, byte[]? key, OneOf<byte[]?, None> value)
        {
            var trackingMap = GetTrackingMap(storeName);
            trackingMap = trackingMap.SetItem(key ?? Array.Empty<byte>(), value);
            UpdateTrackingMap(storeName, trackingMap);
        }

        internal void Update(string? storeName, TrackingMap changes)
        {
            var trackingMap = GetTrackingMap(storeName);
            foreach (var change in changes)
            {
                trackingMap.SetItem(change.Key, change.Value);
            }
            UpdateTrackingMap(storeName, trackingMap);
        }

        void UpdateTrackingMap(string? storeName, TrackingMap changes)
        {
            if (storeName == null)
            {
                defaultTrackingMap = changes;
            }
            else
            {
                trackingMaps = trackingMaps.SetItem(storeName, changes);
            }
        }
    }
}