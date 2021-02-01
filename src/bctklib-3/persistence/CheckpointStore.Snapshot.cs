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
        class Snapshot : ISnapshot
        {
            readonly CheckpointStore store;
            readonly TrackingMap trackingMap;
            TrackingMap writeBatchMap = CheckpointStore.EMPTY_TRACKING_MAP;

            public Snapshot(CheckpointStore store)
            {
                this.store = store;
                this.trackingMap = store.trackingMap;
            }

            public void Dispose()
            {
            }

            byte[]? IReadOnlyStore.TryGet(byte[]? key)
            {
                return store.TryGet(trackingMap, key);
            }

            bool IReadOnlyStore.Contains(byte[]? key)
            {
                return store.Contains(trackingMap, key);
            }

            IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte[]? key, SeekDirection direction)
            {
                return store.Seek(trackingMap, key, direction);
            }

            void ISnapshot.Put(byte[]? key, byte[] value)
            {
                writeBatchMap = writeBatchMap.SetItem(key ?? Array.Empty<byte>(), value);
            }

            void ISnapshot.Delete(byte[]? key)
            {
                writeBatchMap = writeBatchMap.SetItem(key ?? Array.Empty<byte>(), CheckpointStore.NONE_INSTANCE);
            }

            void ISnapshot.Commit()
            {
                var trackingMap = store.trackingMap;
                foreach (var change in writeBatchMap)
                {
                    trackingMap = trackingMap.SetItem(change.Key, change.Value);
                }
                store.trackingMap = trackingMap;
            }
        }
    }
}
