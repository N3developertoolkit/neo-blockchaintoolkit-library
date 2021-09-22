using System;
using System.Diagnostics.CodeAnalysis;
using Neo.Persistence;
using Neo.Plugins;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class CheckpointStorageProvider : IDisposableStorageProvider
    {
        readonly IStorageProvider? storageProvider;
        readonly IDisposable? checkpointCleanup;

        public CheckpointStorageProvider(RocksDbStorageProvider? rocksDbStorageProvider, IDisposable? checkpointCleanup = null)
            : this((IStorageProvider?)rocksDbStorageProvider, checkpointCleanup)
        {
        }

        public CheckpointStorageProvider(IStorageProvider? storageProvider, IDisposable? checkpointCleanup = null)
        {
            this.storageProvider = storageProvider;
            this.checkpointCleanup = checkpointCleanup;
        }

        public void Dispose()
        {
            (storageProvider as IDisposable)?.Dispose();
            checkpointCleanup?.Dispose();
        }

        public IStore GetStore(string? storeName)
        {
            IReadOnlyStore roStore = storageProvider != null && TryGetStore(storageProvider, storeName, out var store)
                ? store
                : NullStore.Instance;
            return new MemoryTrackingStore(roStore);
        }

        static bool TryGetStore(IStorageProvider storageProvider, string? path, [MaybeNullWhen(false)] out IStore store)
        {
            try
            {
                store = storageProvider.GetStore(path);
                return true;
            }
            catch
            {
                store = null;
                return false;
            }
        }
    }
}