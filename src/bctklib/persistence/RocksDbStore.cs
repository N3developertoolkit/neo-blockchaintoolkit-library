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

        internal RocksDbStore(RocksDb db, string? columnFamilyName = null, bool readOnly = false)
            : this(db, GetColumnFamilyHandle(db, columnFamilyName), readOnly, false)
        {
        }

        internal RocksDbStore(RocksDb db, ColumnFamilyHandle columnFamily, bool readOnly = false)
            : this(db, columnFamily, readOnly, false)
        {
        }

        internal RocksDbStore(RocksDb db, ColumnFamilyHandle columnFamily, bool readOnly = false, bool shared = false)
        {
            this.db = db;
            this.columnFamily = columnFamily;
            this.readOnly = readOnly;
            this.shared = shared;
        }

        static ColumnFamilyHandle GetColumnFamilyHandle(RocksDb db, string? columnFamilyName)
        {
            return string.IsNullOrEmpty(columnFamilyName)
                ? db.GetDefaultColumnFamily() : db.GetColumnFamily(columnFamilyName);
        }

        public void Dispose()
        {
            disposed = true;
            if (!shared) db.Dispose();
        }

        public byte[]? TryGet(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            return db.Get(key ?? Array.Empty<byte>(), columnFamily, readOptions);
        }

        public bool Contains(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            return TryGet(key) != null;
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            return db.Seek(columnFamily, key, direction, readOptions);
        }

        public void Put(byte[]? key, byte[]? value)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly) throw new InvalidOperationException("read only");
            db.Put(key ?? Array.Empty<byte>(), value, columnFamily, writeOptions);
        }

        public void PutSync(byte[]? key, byte[]? value)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly) throw new InvalidOperationException("read only");
            db.Put(key ?? Array.Empty<byte>(), value, columnFamily, writeSyncOptions);
        }

        public void Delete(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly) throw new InvalidOperationException("read only");
            db.Remove(key ?? Array.Empty<byte>(), columnFamily, writeOptions);
        }

        public ISnapshot GetSnapshot()
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly) throw new InvalidOperationException("read only");
            return new Snapshot(db, columnFamily);
        }
    }
}