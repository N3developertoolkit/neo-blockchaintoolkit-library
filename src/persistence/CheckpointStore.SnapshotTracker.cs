using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.Persistence;
using OneOf;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    using SeekDirection = Neo.IO.Caching.SeekDirection;
    using TrackingMap = ImmutableSortedDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;
    using WriteBatchMap = ConcurrentDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    public partial class CheckpointStore
    {
        class SnapshotTracker
        {
            private readonly IReadOnlyStore store;
            private readonly byte table;
            private readonly TrackingMap trackingMap;
            private readonly WriteBatchMap writeBatch = new WriteBatchMap(ByteArrayComparer.Default);

            public SnapshotTracker(IReadOnlyStore store, byte table, TrackingMap trackingMap)
            {
                this.store = store;
                this.table = table;
                this.trackingMap = trackingMap;
            }

            public byte[]? TryGet(byte[]? key)
                => CheckpointStore.TryGet(store, table, key, trackingMap);

            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? prefix, SeekDirection direction)
                => CheckpointStore.Seek(store, table, prefix, direction, trackingMap);

            public void Update(byte[]? key, OneOf<byte[], OneOf.Types.None> value)
                => writeBatch[key ?? Array.Empty<byte>()] = value;

            public void Commit(DataTracker dataTracker)
            {
                foreach (var kvp in writeBatch)
                {
                    dataTracker.Update(kvp.Key, kvp.Value);
                }
            }
        }
    }
}
