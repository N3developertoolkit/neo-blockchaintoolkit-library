using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.Persistence;
using OneOf;
using None = OneOf.Types.None;
namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableSortedDictionary<ReadOnlyMemory<byte>, OneOf<ReadOnlyMemory<byte>, None>>;

    public partial class MemoryTrackingStore : IStore
    {
        readonly IReadOnlyStore store;
        TrackingMap trackingMap = TrackingMap.Empty.WithComparers(ReadOnlyMemoryComparer.Default);

        public MemoryTrackingStore(IReadOnlyStore store)
        {
            this.store = store;
        }

        public void Dispose()
        {
            // (store as IDisposable)?.Dispose();
        }

        public bool Contains(byte[] key) => TryGet(key) != null;

        public byte[]? TryGet(byte[] key) => trackingMap.TryGet(store, key);

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
            => trackingMap.Seek(store, key, direction);

        public void Put(byte[]? key, byte[]? value)
        {
            trackingMap = trackingMap.SetItem(key.CloneReadOnlyMemory(), value.CloneReadOnlyMemory());
        }

        public void Delete(byte[]? key)
        {
            trackingMap = trackingMap.SetItem(key.CloneReadOnlyMemory(), default(None));
        }

        public ISnapshot GetSnapshot()
        {
            return new Snapshot(store, trackingMap, this.Update);
        }

        void Update(TrackingMap writeBatchMap)
        {
            var updatedTrackingMap = trackingMap;
            foreach (var change in writeBatchMap)
            {
                updatedTrackingMap = updatedTrackingMap.SetItem(change.Key, change.Value);
            }
            trackingMap = updatedTrackingMap;
        }
    }
}