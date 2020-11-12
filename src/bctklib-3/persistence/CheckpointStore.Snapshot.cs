using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.IO.Caching;
using Neo.Persistence;
using OneOf;

namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableSortedDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    public partial class CheckpointStore
    {
        class Snapshot : ISnapshot
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

            byte[]? IReadOnlyStore.TryGet(byte table, byte[]? key)
            {
                var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
                return store.TryGet(trackingMap, table, key);
            }

            bool IReadOnlyStore.Contains(byte table, byte[] key)
            {
                var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
                return store.Contains(trackingMap, table, key);
            }

            IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte table, byte[] prefix, SeekDirection direction)
            {
                var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
                return store.Seek(trackingMap, table, prefix, direction);
            }

            void ISnapshot.Put(byte table, byte[]? key, byte[] value)
            {
                var map = writeBatchMap.GetValueOrDefault(table, EMPTY_TRACKING_MAP) ?? throw new NullReferenceException();
                writeBatchMap[table] = map.SetItem(key ?? Array.Empty<byte>(), value);
            }

            void ISnapshot.Delete(byte table, byte[]? key)
            {
                var map = writeBatchMap.GetValueOrDefault(table, EMPTY_TRACKING_MAP) ?? throw new NullReferenceException();
                writeBatchMap[table] = map.SetItem(key ?? Array.Empty<byte>(), NONE_INSTANCE);
            }

            void ISnapshot.Commit()
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
        }
    }
}
