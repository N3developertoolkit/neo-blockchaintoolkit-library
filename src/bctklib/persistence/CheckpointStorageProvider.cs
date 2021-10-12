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
        readonly bool disposeStorageProvider;

        ImmutableDictionary<string, IStore> stores = ImmutableDictionary<string, IStore>.Empty;

        public CheckpointStorageProvider(RocksDbStorageProvider? rocksDbStorageProvider, bool disposeStorageProvider = true, IDisposable? checkpointCleanup = null)
            : this((IStorageProvider?)rocksDbStorageProvider, disposeStorageProvider, checkpointCleanup)
        {
        }

        public CheckpointStorageProvider(IStorageProvider? storageProvider, bool disposeStorageProvider = true, IDisposable? checkpointCleanup = null)
        {
            this.storageProvider = storageProvider;
            this.checkpointCleanup = checkpointCleanup;
            this.disposeStorageProvider = disposeStorageProvider;

            defaultStore = new Lazy<IStore>(() => new MemoryTrackingStore(GetStorageProviderStore(null)));
        }

        public void Dispose()
        {
            if (disposeStorageProvider)
            {
                (storageProvider as IDisposable)?.Dispose();
            }
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
            catch { }

            return roStore ?? NullStore.Instance;
        }
    }
}