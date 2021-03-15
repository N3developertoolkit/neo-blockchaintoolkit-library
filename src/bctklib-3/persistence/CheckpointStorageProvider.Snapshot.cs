using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.Persistence;
using OneOf;

namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableSortedDictionary<byte[], OneOf<byte[]?, OneOf.Types.None>>;

    public partial class CheckpointStorageProvider
    {
        class Snapshot : ISnapshot
        {
            readonly CheckpointStorageProvider provider;
            readonly TrackingMap trackingMap;
            readonly string storeName;
            TrackingMap writeBatchMap = CheckpointStorageProvider.EMPTY_TRACKING_MAP;

            public Snapshot(CheckpointStorageProvider provider, TrackingMap trackingMap, string storeName)
            {
                this.provider = provider;
                this.trackingMap = trackingMap;
                this.storeName = storeName;
            }

            public void Dispose()
            {
            }

            public byte[]? TryGet(byte[]? key) => provider.TryGet(storeName, trackingMap, key);

            public bool Contains(byte[]? key) => TryGet(key) != null;

            public IEnumerable<(byte[] Key, byte[]? Value)> Seek(byte[]? key, SeekDirection direction) => provider.Seek(storeName, trackingMap, key, direction);

            public void Put(byte[]? key, byte[]? value)
            {
                writeBatchMap = writeBatchMap.SetItem(key ?? Array.Empty<byte>(), value);
            }

            public void Delete(byte[] key)
            {
                writeBatchMap = writeBatchMap.SetItem(key ?? Array.Empty<byte>(), new OneOf.Types.None());
            }

            public void Commit()
            {
                provider.Update(storeName, writeBatchMap);
            }
        }
    }
}