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
            private readonly CheckpointStore store;
            private readonly ConcurrentDictionary<byte, SnapshotTracker> snapshotTrackers = new ConcurrentDictionary<byte, SnapshotTracker>();

            public Snapshot(CheckpointStore store)
            {
                this.store = store;
                foreach (var kvp in store.dataTrackers)
                {
                    snapshotTrackers.TryAdd(kvp.Key, kvp.Value.GetSnapshot());
                }
            }

            public void Dispose()
            {
            }

            SnapshotTracker GetSnapshotTracker(byte table)
                => snapshotTrackers.GetOrAdd(table, t => new SnapshotTracker(store.store, t, TrackingMap.Empty));

            byte[]? IReadOnlyStore.TryGet(byte table, byte[]? key)
                => GetSnapshotTracker(table).TryGet(key);

            bool IReadOnlyStore.Contains(byte table, byte[] key)
                => null != GetSnapshotTracker(table).TryGet(key);

            IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte table, byte[] prefix, SeekDirection direction)
               => GetSnapshotTracker(table).Seek(prefix, direction);

            void ISnapshot.Put(byte table, byte[]? key, byte[] value)
                => GetSnapshotTracker(table).Update(key, value);

            void ISnapshot.Delete(byte table, byte[] key)
                => GetSnapshotTracker(table).Update(key, CheckpointStore.NONE_INSTANCE);

            void ISnapshot.Commit()
            {
                foreach (var kvp in snapshotTrackers)
                {
                    var tracker = store.GetDataTracker(kvp.Key);
                    kvp.Value.Commit(tracker);
                }
            }
        }
    }
}
