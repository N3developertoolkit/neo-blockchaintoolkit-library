using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.Persistence;
using OneOf;
using None = OneOf.Types.None;
namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableSortedDictionary<ReadOnlyMemory<byte>, OneOf<ReadOnlyMemory<byte>, None>>;

    public partial class MemoryTrackingStore
    {
        class Snapshot : ISnapshot
        {
            readonly IReadOnlyStore store;
            readonly TrackingMap trackingMap;
            readonly Action<TrackingMap> updateAction;
            TrackingMap writeBatchMap = TrackingMap.Empty.WithComparers(ReadOnlyMemoryComparer.Default);

            public Snapshot(IReadOnlyStore store, TrackingMap trackingMap, Action<TrackingMap> updateAction)
            {
                this.store = store;
                this.trackingMap = trackingMap;
                this.updateAction = updateAction;
            }

            public void Dispose() { }

            public bool Contains(byte[] key) => TryGet(key) != null;

            public byte[]? TryGet(byte[] key) => trackingMap.TryGet(store, key);

            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
                => trackingMap.Seek(store, key, direction);

            public void Put(byte[]? key, byte[] value)
            {
                var _key = key == null ? default : key.AsSpan().ToArray();
                ReadOnlyMemory<byte> _value = value == null ? default : value.AsSpan().ToArray();
                writeBatchMap = writeBatchMap.SetItem(_key, _value);
            }

            public void Delete(byte[]? key)
            {
                var _key = key == null ? default : key.AsSpan().ToArray();
                writeBatchMap = writeBatchMap.SetItem(_key, default(None));
            }

            public void Commit() => updateAction(writeBatchMap);
        }
    }
}