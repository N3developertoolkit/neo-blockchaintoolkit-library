using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Neo.Persistence;
using Neo.Plugins;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class CheckpointStorageProvider : IDisposableStorageProvider
    {
        readonly IStorageProvider? storageProvider;
        readonly IDisposable? checkpointCleanup;
        readonly Lazy<IStore> defaultStore;
        ImmutableDictionary<string, IStore> stores = ImmutableDictionary<string, IStore>.Empty;

        public CheckpointStorageProvider(RocksDbStorageProvider? rocksDbStorageProvider, IDisposable? checkpointCleanup = null)
            : this((IStorageProvider?)rocksDbStorageProvider, checkpointCleanup)
        {
        }

        public CheckpointStorageProvider(IStorageProvider? storageProvider, IDisposable? checkpointCleanup = null)
        {
            this.storageProvider = storageProvider;
            this.checkpointCleanup = checkpointCleanup;

            defaultStore = new Lazy<IStore>(() => new MemoryTrackingStore(GetStorageProviderStore(null)));
        }

        public void Dispose()
        {
            (storageProvider as IDisposable)?.Dispose();
            checkpointCleanup?.Dispose();
        }

        public IStore GetStore(string? storeName)
        {
            if (storeName == null) return defaultStore.Value;
            return ImmutableInterlocked.GetOrAdd(ref stores, storeName, 
                key => new MemoryTrackingStore(GetStorageProviderStore(key)));
        }

        IReadOnlyStore GetStorageProviderStore(string? path)
        {
            IReadOnlyStore? roStore = null;
            try
            {
                roStore = storageProvider?.GetStore(path);
            }
            catch {}

            return roStore ?? NullStore.Instance;
        }
    }
}