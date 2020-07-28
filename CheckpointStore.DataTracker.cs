using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.Persistence;
using OneOf;

namespace Neo.Seattle.Persistence
{
    using SeekDirection = Neo.IO.Caching.SeekDirection;
    using TrackingMap = ImmutableSortedDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    public partial class CheckpointStore
    {
        partial class DataTracker
        {
            private readonly IReadOnlyStore store;
            private readonly byte table;
            private TrackingMap trackingMap = TrackingMap.Empty.WithComparers(ByteArrayComparer.Default);

            public DataTracker(IReadOnlyStore store, byte table)
            {
                this.store = store;
                this.table = table;
            }

            public SnapshotTracker GetSnapshot()
                => new SnapshotTracker(this.store, this.table, this.trackingMap);

            public byte[]? TryGet(byte[]? key)
                => CheckpointStore.TryGet(store, table, key, trackingMap);

            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? prefix, SeekDirection direction)
                => CheckpointStore.Seek(store, table, prefix, direction, trackingMap);

            public void Update(byte[]? key, OneOf<byte[], OneOf.Types.None> value)
            {
                trackingMap = trackingMap.SetItem(key ?? Array.Empty<byte>(), value);
            }
        }
    }
}
