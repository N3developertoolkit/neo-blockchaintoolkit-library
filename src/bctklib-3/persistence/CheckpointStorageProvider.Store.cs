using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.Persistence;
using OneOf;

namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableSortedDictionary<byte[], OneOf<byte[]?, OneOf.Types.None>>;

    public partial class CheckpointStorageProvider
    {
        public class Store : IStore
        {
            readonly CheckpointStorageProvider provider;
            readonly string storeName;

            public Store(CheckpointStorageProvider storageProvider, string storagePath)
            {
                this.provider = storageProvider;
                this.storeName = storagePath;
            }

            public void Dispose() { }

            public byte[]? TryGet(byte[]? key) => provider.TryGet(storeName, key);

            public bool Contains(byte[]? key) => TryGet(key) != null;

            public IEnumerable<(byte[] Key, byte[]? Value)> Seek(byte[]? key, SeekDirection direction) => provider.Seek(storeName, key, direction);

            public void Put(byte[]? key, byte[]? value) => provider.Update(storeName, key, value);

            public void Delete(byte[]? key) => provider.Update(storeName, key, new OneOf.Types.None());

            public ISnapshot GetSnapshot() => provider.GetSnapshot(storeName);
        }
    }
}