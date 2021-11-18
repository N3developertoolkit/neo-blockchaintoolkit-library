using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Neo.BlockchainToolkit.Models;
using Neo.Persistence;
using Neo.Plugins;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class RocksDbStorageProvider : IStorageProvider, IDisposable
    {
        static readonly ColumnFamilyOptions defaultColumnFamilyOptions = new ColumnFamilyOptions();

        readonly RocksDb db;
        readonly bool readOnly;

        internal RocksDbStorageProvider(RocksDb db, bool readOnly)
        {
            this.db = db;
            this.readOnly = readOnly;
        }

        public static IStorageProvider Open(string path)
        {
            var db = RocksDbUtility.OpenDb(path);
            return new RocksDbStorageProvider(db, readOnly: false);
        }

        public static IStorageProvider OpenForDiscard(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try
                {
                    var db = RocksDbUtility.OpenReadOnlyDb(path);
                    var storageProvider = new RocksDbStorageProvider(db, readOnly: true);
                    return new CheckpointStorageProvider(string.Empty, storageProvider);
                }
                catch { }
            }

            return new CheckpointStorageProvider(string.Empty, null);
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public IStore GetStore(string? path)
        {
            if (path == null) 
                return new RocksDbStore(db, db.GetDefaultColumnFamily(), readOnly, shared: true);

            if (db.TryGetColumnFamily(path, out var columnFamily)) 
                return new RocksDbStore(db, columnFamily, readOnly, shared: true);


            if (!readOnly)
            {
                columnFamily = db.CreateColumnFamily(defaultColumnFamilyOptions, path);
                return new RocksDbStore(db, columnFamily, readOnly, shared: true);
            }

            throw new InvalidOperationException("invalid store path");
        }

        public void CreateCheckpoint(string checkPointFileName, ProtocolSettings settings, UInt160 scriptHash)
            => CreateCheckpoint(checkPointFileName, settings.Network, settings.AddressVersion, scriptHash);

        public void CreateCheckpoint(string checkPointFileName, ExpressChain chain, UInt160 scriptHash)
            => CreateCheckpoint(checkPointFileName, chain.Network, chain.AddressVersion, scriptHash);

        public void CreateCheckpoint(string checkPointFileName, uint network, byte addressVersion, UInt160 scriptHash) 
            => RocksDbUtility.CreateCheckpoint(db, checkPointFileName, network, addressVersion, scriptHash);
    }
}