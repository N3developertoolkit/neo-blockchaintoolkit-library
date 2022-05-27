using System;
using System.Collections.Immutable;
using System.IO;
using Neo.Persistence;
using Neo.Plugins;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class CheckpointStorageProvider : IStorageProvider, IDisposable
    {
        readonly IStorageProvider? underlyingStorageProvider;
        readonly string checkpointTempPath;
        readonly Lazy<IStore> defaultStore;
        ImmutableDictionary<string, IStore> stores = ImmutableDictionary<string, IStore>.Empty;

        internal CheckpointStorageProvider(IStorageProvider? underlyingStorageProvider, string checkpointTempPath = "")
        {
            this.checkpointTempPath = checkpointTempPath;
            this.underlyingStorageProvider = underlyingStorageProvider;
            defaultStore = new Lazy<IStore>(() => new MemoryTrackingStore(GetUnderlyingStore(null)));
        }

        public static CheckpointStorageProvider Open(string checkpointPath, uint? network = null, byte? addressVersion = null, UInt160? scriptHash = null)
        {
            var checkpointTempPath = RocksDbUtility.GetTempPath();
            var metadata = RocksDbUtility.RestoreCheckpoint(checkpointPath, checkpointTempPath, network, addressVersion, scriptHash);

            var db = RocksDbUtility.OpenReadOnlyDb(checkpointTempPath);
            var rocksDbStorageProvider = new RocksDbStorageProvider(db, readOnly: true);
            return new CheckpointStorageProvider(rocksDbStorageProvider, checkpointTempPath);
        }

        public void Dispose()
        {
            (underlyingStorageProvider as IDisposable)?.Dispose();

            if (!string.IsNullOrEmpty(checkpointTempPath)
                && Directory.Exists(checkpointTempPath))
            {
                Directory.Delete(checkpointTempPath, true);
            }
        }

        public IStore GetStore(string? path)
        {
            if (path == null) return defaultStore.Value;
            return ImmutableInterlocked.GetOrAdd(ref stores, path,
                key => new MemoryTrackingStore(GetUnderlyingStore(key)));
        }

        IReadOnlyStore GetUnderlyingStore(string? path)
        {
            IReadOnlyStore? roStore = null;
            try
            {
                roStore = underlyingStorageProvider?.GetStore(path);
            }
            catch { }

            return roStore ?? NullStore.Instance;
        }
    }
}