using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.Persistence;
using OneOf;

namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableSortedDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    public partial class CheckpointStore
    {
        class Snapshot : ISnapshot, IExpressSnapshot
        {
            readonly CheckpointStore store;
            readonly ImmutableDictionary<byte, TrackingMap> trackingMaps;
            Dictionary<byte, TrackingMap> writeBatchMap = new Dictionary<byte, TrackingMap>();

            public Snapshot(CheckpointStore store)
            {
                this.store = store;
                trackingMaps = store.trackingMaps;
            }

            public void Dispose()
            {
            }

            // SnapshotTracker GetSnapshotTracker(byte table)
            //     => snapshotTrackers.GetOrAdd(table, t => new SnapshotTracker(store.store, t, TrackingMap.Empty));

            public byte[]? TryGet(byte table, byte[]? key)
            {
                var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
                return store.TryGet(trackingMap, table, key);
            }

            public bool Contains(byte table, byte[]? key)
            {
                var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
                return store.Contains(trackingMap, table, key);
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[]? prefix, SeekDirection direction)
            {
                var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
                return store.Seek(trackingMap, table, prefix, direction);
            }

            public void Put(byte table, byte[]? key, byte[] value)
            {
                var map = writeBatchMap.GetValueOrDefault(table, EMPTY_TRACKING_MAP) ?? throw new NullReferenceException();
                writeBatchMap[table] = map.SetItem(key ?? Array.Empty<byte>(), value);
            }

            public void Delete(byte table, byte[]? key)
            {
                var map = writeBatchMap.GetValueOrDefault(table, EMPTY_TRACKING_MAP) ?? throw new NullReferenceException();
                writeBatchMap[table] = map.SetItem(key ?? Array.Empty<byte>(), NONE_INSTANCE);
            }

            public void Commit()
            {
                var trackingMaps = store.trackingMaps;
                foreach (var (table, changes) in writeBatchMap)
                {
                    var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
                    foreach (var change in changes)
                    {
                        trackingMap = trackingMap.SetItem(change.Key, change.Value);
                    }
                    trackingMaps = trackingMaps.SetItem(table, trackingMap);
                }
                store.trackingMaps = trackingMaps;
            }

            byte[]? IReadOnlyStore.TryGet(byte[]? key) => TryGet(default, key);
            bool IReadOnlyStore.Contains(byte[]? key) => Contains(default, key);
            IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte[]? key, SeekDirection direction) => Seek(default, key, direction);
            void ISnapshot.Put(byte[]? key, byte[] value) => Put(default, key, value);
            void ISnapshot.Delete(byte[]? key) => Delete(default, key);
        }
    }
}