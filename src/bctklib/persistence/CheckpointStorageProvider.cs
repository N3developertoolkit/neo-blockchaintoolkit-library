using System;
using System.Collections.Immutable;
using System.IO;
using Neo.Persistence;
using Neo.Plugins;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class CheckpointStorageProvider : IStorageProvider, IDisposable
    {
        readonly RocksDbStorageProvider? underlyingStorageProvider;
        readonly string checkpointTempPath;
        readonly Lazy<IStore> defaultStore;
        ImmutableDictionary<string, IStore> stores = ImmutableDictionary<string, IStore>.Empty;

        internal CheckpointStorageProvider(string checkpointTempPath, RocksDbStorageProvider? underlyingStorageProvider)
        {
            this.checkpointTempPath = checkpointTempPath;
            this.underlyingStorageProvider = underlyingStorageProvider;
            defaultStore = new Lazy<IStore>(() => new MemoryTrackingStore(GetUnderlyingReadOnlyStore(null)));
        }

        public static CheckpointStorageProvider Open(string checkpointPath, uint? network = null, byte? addressVersion = null, UInt160? scriptHash = null)
        {
            var checkpointTempPath = RocksDbUtility.GetTempPath();
            var metadata = RocksDbUtility.RestoreCheckpoint(checkpointPath, checkpointTempPath, network, addressVersion, scriptHash);

            var db = RocksDbUtility.OpenReadOnlyDb(checkpointTempPath);
            var rocksDbStorageProvider = new RocksDbStorageProvider(db, readOnly: true);
            return new CheckpointStorageProvider(checkpointTempPath, rocksDbStorageProvider);
        }

        public void Dispose()
        {
            underlyingStorageProvider?.Dispose();

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
                key => new MemoryTrackingStore(GetUnderlyingReadOnlyStore(key)));
        }

        IReadOnlyStore GetUnderlyingReadOnlyStore(string? path)
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