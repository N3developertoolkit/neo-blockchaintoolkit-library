using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.Persistence;
using OneOf;
using None = OneOf.Types.None;
namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableDictionary<ReadOnlyMemory<byte>, OneOf<ReadOnlyMemory<byte>, None>>;

    public partial class MemoryTrackingStore
    {
        class Snapshot : ISnapshot
        {
            readonly IReadOnlyStore store;
            readonly TrackingMap trackingMap;
            readonly Action<TrackingMap> commitAction;
            TrackingMap writeBatchMap = TrackingMap.Empty.WithComparers(ReadOnlyMemoryComparer.Default);

            public Snapshot(IReadOnlyStore store, TrackingMap trackingMap, Action<TrackingMap> commitAction)
            {
                this.store = store;
                this.trackingMap = trackingMap;
                this.commitAction = commitAction;
            }

            public void Dispose() { }

            public bool Contains(byte[]? key) => TryGet(key) != null;

            public byte[]? TryGet(byte[]? key) => trackingMap.TryGet(store, key);

            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
                => trackingMap.Seek(store, key, direction);

            public void Put(byte[]? key, byte[] value)
            {
                MemoryTrackingStore.AtomicUpdate(ref writeBatchMap, key, value);
            }

            public void Delete(byte[]? key)
            {
                MemoryTrackingStore.AtomicUpdate(ref writeBatchMap, key, default(None));
            }

            public void Commit() => commitAction(writeBatchMap);
        }
    }
}