using System;
using System.Collections.Generic;
using Neo.Persistence;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class RocksDbStore : IStore
    {
        readonly RocksDb db;
        readonly ColumnFamilyHandle columnFamily;
        readonly bool readOnly;
        readonly bool shared;
        bool disposed;
        readonly ReadOptions readOptions = new ReadOptions();
        readonly WriteOptions writeOptions = new WriteOptions();
        readonly WriteOptions writeSyncOptions = new WriteOptions().SetSync(true);

        public RocksDbStore(RocksDb db, bool readOnly = false, bool shared = false)
            : this(db, db.GetDefaultColumnFamily(), readOnly, shared)
        {
        }

        public RocksDbStore(RocksDb db, ColumnFamilyHandle columnFamily, bool readOnly = false, bool shared = false)
        {
            this.db = db;
            this.columnFamily = columnFamily;
            this.readOnly = readOnly;
            this.shared = shared;
        }

        public static RocksDbStore Open(string path)
        {
            var db = RocksDbUtility.OpenDb(path);
            return new RocksDbStore(db, readOnly: false);
        }

        public static RocksDbStore OpenReadOnly(string path)
        {
            var db = RocksDbUtility.OpenReadOnlyDb(path);
            return new RocksDbStore(db, readOnly: true);
        }


        public void Dispose()
        {
            disposed = true;
            if (!shared) db.Dispose();
        }

        public byte[]? TryGet(byte[]? key)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RocksDbStore));
            return db.Get(key ?? Array.Empty<byte>(), columnFamily, readOptions);
        }

        public bool Contains(byte[]? key)
        {
            return TryGet(key) != null;
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RocksDbStore));
            return db.Seek(columnFamily, key, direction, readOptions);
        }

        public void Put(byte[]? key, byte[] value)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly) throw new InvalidOperationException("read only");
            db.Put(key ?? Array.Empty<byte>(), value, columnFamily, writeOptions);
        }

        public void PutSync(byte[]? key, byte[] value)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly) throw new InvalidOperationException("read only");
            db.Put(key ?? Array.Empty<byte>(), value, columnFamily, writeSyncOptions);
        }

        public void Delete(byte[]? key)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly) throw new InvalidOperationException("read only");
            db.Remove(key ?? Array.Empty<byte>(), columnFamily, writeOptions);
        }

        public ISnapshot GetSnapshot()
        {
            if (disposed) throw new ObjectDisposedException(nameof(RocksDbStore));
            return new Snapshot(db, columnFamily);
        }
    }
}