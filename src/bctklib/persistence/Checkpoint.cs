using System;
using System.Collections.Generic;
using System.IO;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public class Checkpoint : ICheckpoint, IDisposable
    {
        readonly RocksDbStore store;
        readonly string checkpointTempPath;

        public ProtocolSettings Settings { get; }

        public Checkpoint(string checkpointPath, uint? network = null, byte? addressVersion = null, UInt160? scriptHash = null)
        {
            checkpointTempPath = RocksDbUtility.GetTempPath();
            var metadata = RocksDbUtility.RestoreCheckpoint(checkpointPath, checkpointTempPath, network, addressVersion, scriptHash);

            Settings = ProtocolSettings.Default with
            {
                Network = metadata.network,
                AddressVersion = metadata.addressVersion,
            };

            var db = RocksDbUtility.OpenReadOnlyDb(checkpointTempPath);
            this.store = new RocksDbStore(db, readOnly: true);
        }

        public void Dispose()
        {
            store.Dispose();

            if (!string.IsNullOrEmpty(checkpointTempPath)
                && Directory.Exists(checkpointTempPath))
            {
                Directory.Delete(checkpointTempPath, true);
            }
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] key, SeekDirection direction) => store.Seek(key, direction);
        public byte[]? TryGet(byte[] key) => store.TryGet(key);
        public bool Contains(byte[] key) => store.Contains(key);
    }
}