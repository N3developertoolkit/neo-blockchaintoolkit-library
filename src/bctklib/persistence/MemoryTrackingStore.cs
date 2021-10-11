using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Neo.Persistence;
using OneOf;
using None = OneOf.Types.None;
namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableSortedDictionary<ReadOnlyMemory<byte>, OneOf<ReadOnlyMemory<byte>, None>>;

    public partial class MemoryTrackingStore : IStore
    {
        readonly IReadOnlyStore store;
        readonly IDisposable? disposable;
        TrackingMap trackingMap = TrackingMap.Empty.WithComparers(ReadOnlyMemoryComparer.Default);

        public MemoryTrackingStore(IReadOnlyStore store, IDisposable? disposable = null)
        {
            this.store = store;
            this.disposable = disposable;
        }

        public void Dispose()
        {
            (store as IDisposable)?.Dispose();
            disposable?.Dispose();
        }

        public ISnapshot GetSnapshot() => new Snapshot(store, trackingMap, this.CommitSnapshot);

        public bool Contains(byte[] key) => TryGet(key) != null;

        public byte[]? TryGet(byte[] key) => trackingMap.TryGet(store, key);

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
            => trackingMap.Seek(store, key, direction);

        public void Put(byte[]? key, byte[]? value)
        {
            AtomicUpdate(ref trackingMap, key.CloneAsReadOnlyMemory(), value.CloneAsReadOnlyMemory());
        }

        public void Delete(byte[]? key)
        {
            AtomicUpdate(ref trackingMap, key.CloneAsReadOnlyMemory(), default(None));
        }

        static void AtomicUpdate(ref TrackingMap trackingMap, ReadOnlyMemory<byte> key, OneOf<ReadOnlyMemory<byte>, None> value)
        {
            var priorCollection = Volatile.Read(ref trackingMap);
            do
            {
                var updatedCollection = priorCollection.SetItem(key, value);
                var interlockedResult = Interlocked.CompareExchange(ref trackingMap, updatedCollection, priorCollection);
                if (object.ReferenceEquals(priorCollection, interlockedResult)) break;
                priorCollection = interlockedResult;
            }
            while (true);
        }

        void CommitSnapshot(TrackingMap writeBatchMap)
        {
            var priorCollection = Volatile.Read(ref trackingMap);
            do
            {
                var updatedCollection = Volatile.Read(ref trackingMap);
                foreach (var change in writeBatchMap)
                {
                    updatedCollection = updatedCollection.SetItem(change.Key, change.Value);
                }

                var interlockedResult = Interlocked.CompareExchange(ref trackingMap, updatedCollection, priorCollection);
                if (object.ReferenceEquals(priorCollection, interlockedResult)) break;
                priorCollection = interlockedResult;
            }
            while (true);
        }
    }
}