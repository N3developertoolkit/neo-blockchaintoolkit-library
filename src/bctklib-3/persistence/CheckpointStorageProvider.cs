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
        public delegate bool TryGetStore(string path, [NotNullWhen(true)] out IStore? store);

        public readonly static TrackingMap EMPTY_TRACKING_MAP = TrackingMap.Empty.WithComparers(ByteArrayComparer.Default);

        readonly RocksDbStorageProvider? rocksDbStorageProvider;
        readonly IDisposable? checkpointCleanup;
        ImmutableDictionary<string, TrackingMap> trackingMaps = ImmutableDictionary<string, TrackingMap>.Empty;

        public CheckpointStorageProvider(RocksDbStorageProvider? rocksDbStorageProvider, IDisposable? checkpointCleanup = null)
        {
            this.rocksDbStorageProvider = rocksDbStorageProvider;
            this.checkpointCleanup = checkpointCleanup;
        }

        internal TrackingMap GetTrackingMap(string path) => trackingMaps.TryGetValue(path, out var map) ? map : EMPTY_TRACKING_MAP;

        internal IReadOnlyStore GetReadOnlyStore(string path)
            => (rocksDbStorageProvider != null && rocksDbStorageProvider.TryGetStore(path, out var _store))
                ? _store
                : NullStore.Instance;

        public IStore GetStore(string path)
        {
            return new Store(this, path);
        }

        internal ISnapshot GetSnapshot(string path)
        {
            var map = GetTrackingMap(path);
            return new CheckpointStorageProvider.Snapshot(this, map, path);
        }

        internal byte[]? TryGet(string path, byte[]? key)
        {
            var map = GetTrackingMap(path);
            return TryGet(path, map, key);
        }

        internal byte[]? TryGet(string path, TrackingMap map, byte[]? key)
        {
            if (map.TryGetValue(key ?? Array.Empty<byte>(), out var mapValue))
            {
                return mapValue.Match<byte[]?>(v => v, n => null);
            }

            return GetReadOnlyStore(path).TryGet(key);
        }

        internal IEnumerable<(byte[] Key, byte[]? Value)> Seek(string path, byte[]? key, SeekDirection direction)
        {
            var map = GetTrackingMap(path);
            return Seek(path, map, key, direction);
        }

        internal IEnumerable<(byte[] Key, byte[]? Value)> Seek(string path, TrackingMap map, byte[]? key, SeekDirection direction)
        {
            key ??= Array.Empty<byte>();
            var comparer = direction == SeekDirection.Forward ? ByteArrayComparer.Default : ByteArrayComparer.Reverse;

            var memoryItems = map
                .Where(kvp => kvp.Value.IsT0)
                .Where(kvp => key.Length == 0 || comparer.Compare(kvp.Key, key) >= 0)
                .Select(kvp => (kvp.Key, Value: kvp.Value.AsT0));

            var storeItems = GetReadOnlyStore(path)
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


        public void Dispose()
        {
            rocksDbStorageProvider?.Dispose();
            checkpointCleanup?.Dispose();
        }

        class NullStore : IReadOnlyStore
        {
            public static NullStore Instance = new NullStore();

            private NullStore() { }

            public bool Contains(byte[]? key) => false;
            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] key, SeekDirection direction) => Enumerable.Empty<(byte[], byte[])>();
            public byte[]? TryGet(byte[]? key) => null;
        }
    }
}