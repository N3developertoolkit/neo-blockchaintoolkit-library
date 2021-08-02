using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Neo.Persistence;
using Neo.Wallets;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{

    public partial class RocksDbStorageProvider
    {
        public class Store : IStore
        {
            readonly RocksDb db;
            readonly ColumnFamilyHandle columnFamily;
            readonly bool readOnly;
            readonly ReadOptions readOptions = new ReadOptions();
            readonly WriteOptions writeOptions = new WriteOptions();
            readonly WriteOptions writeSyncOptions = new WriteOptions().SetSync(true);

            internal Store(RocksDb db, ColumnFamilyHandle columnFamily, bool readOnly)
            {
                this.db = db;
                this.columnFamily = columnFamily;
                this.readOnly = readOnly;
            }

            public void Dispose()
            {
            }

            public byte[]? TryGet(byte[]? key)
            {
                return db.Get(key ?? Array.Empty<byte>(), columnFamily, readOptions);
            }

            public bool Contains(byte[]? key)
            {
                return TryGet(key) != null;
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
            {
                return RocksDbStorageProvider.Seek(db, columnFamily, key, direction, readOptions);
            }

            public void Put(byte[]? key, byte[]? value)
            {
                if (readOnly) throw new InvalidOperationException("read only");
                db.Put(key ?? Array.Empty<byte>(), value, columnFamily, writeOptions);
            }

            public void PutSync(byte[]? key, byte[]? value)
            {
                if (readOnly) throw new InvalidOperationException("read only");
                db.Put(key ?? Array.Empty<byte>(), value, columnFamily, writeSyncOptions);
            }

            public void Delete(byte[] key)
            {
                if (readOnly) throw new InvalidOperationException("read only");
                db.Remove(key ?? Array.Empty<byte>(), columnFamily, writeOptions);
            }

            public ISnapshot GetSnapshot()
            {
                return new RocksDbStorageProvider.Snapshot(db, columnFamily);
            }
        }
    }
}