using System;
using System.Diagnostics.CodeAnalysis;
using Neo.BlockchainToolkit.Models;
using Neo.Persistence;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class RocksDbStorageProvider : IDisposableStorageProvider
    {
        static readonly ColumnFamilyOptions defaultColumnFamilyOptions = new ColumnFamilyOptions();

        readonly RocksDb db;
        readonly bool readOnly;

        RocksDbStorageProvider(RocksDb db, bool readOnly)
        {
            this.db = db;
            this.readOnly = readOnly;
        }

        public static RocksDbStorageProvider Open(string path)
        {
            var db = RocksDbStore.OpenDb(path);
            return new RocksDbStorageProvider(db, readOnly: false);
        }

        public static RocksDbStorageProvider OpenReadOnly(string path)
        {
            var db = RocksDbStore.OpenReadOnlyDb(path);
            return new RocksDbStorageProvider(db, readOnly: true);
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public IStore GetStore(string? path)
        {
            return TryGetStore(path, out var store) 
                ? store 
                : throw new InvalidOperationException("invalid store path");
        }

        public bool TryGetStore(string? path, [NotNullWhen(true)] out IStore? store)
        {
            if (path == null)
            {
                store = new RocksDbStore(db, db.GetDefaultColumnFamily(), readOnly, shared: true);
                return true;
            }

            if (db.TryGetColumnFamily(path, out var columnFamily))
            {
                store = new RocksDbStore(db, columnFamily, readOnly, shared: true);
                return true;
            }

            if (!readOnly)
            {
                columnFamily = db.CreateColumnFamily(defaultColumnFamilyOptions, path);
                store = new RocksDbStore(db, columnFamily, readOnly, shared: true);
                return true;
            }

            store = null;
            return false;
        }

        public void CreateCheckpoint(string checkPointFileName, ProtocolSettings settings, UInt160 scriptHash)
            => CreateCheckpoint(checkPointFileName, settings.Network, settings.AddressVersion, scriptHash);

        public void CreateCheckpoint(string checkPointFileName, ExpressChain chain, UInt160 scriptHash)
            => CreateCheckpoint(checkPointFileName, chain.Network, chain.AddressVersion, scriptHash);

        public void CreateCheckpoint(string checkPointFileName, uint magic, byte addressVersion, UInt160 scriptHash)
        {
            Checkpoint.Create(db, checkPointFileName, magic, addressVersion, scriptHash);
        }

        [Obsolete("use Checkpoint.Restore instead")]
        public static (uint magic, byte addressVersion) RestoreCheckpoint(string checkPointArchive, string restorePath, ProtocolSettings settings, UInt160 scriptHash)
            => RestoreCheckpoint(checkPointArchive, restorePath, settings.Network, settings.AddressVersion, scriptHash);

        [Obsolete("use Checkpoint.Restore instead")]
        public static (uint magic, byte addressVersion) RestoreCheckpoint(string checkPointArchive, string restorePath, uint magic, byte addressVersion, UInt160 scriptHash)
        {
            return Checkpoint.Restore(checkPointArchive, restorePath, magic, addressVersion, scriptHash);
        }

        [Obsolete("use Checkpoint.Restore instead")]
        public static (uint magic, byte addressVersion) RestoreCheckpoint(string checkPointArchive, string restorePath)
        {
            return Checkpoint.Restore(checkPointArchive, restorePath);
        }
    }
}