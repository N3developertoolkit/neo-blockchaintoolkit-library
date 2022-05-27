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

        internal RocksDbStore(RocksDb db, bool readOnly = false)
            : this(db, db.GetDefaultColumnFamily(), readOnly, false)
        {
        }

        public RocksDbStore(RocksDb db, string? columnFamilyName, bool readOnly = false)
            : this(db, db.GetColumnFamilyOrDefault(columnFamilyName), readOnly, false)
        {
        }

        public RocksDbStore(RocksDb db, ColumnFamilyHandle columnFamily, bool readOnly = false)
            : this(db, columnFamily, readOnly, false)
        {
        }

        public RocksDbStore(RocksDb db, ColumnFamilyHandle columnFamily, bool readOnly, bool shared)
        {
            this.db = db;
            this.columnFamily = columnFamily;
            this.readOnly = readOnly;
            this.shared = shared;
        }

        public void Dispose()
        {
            if (disposed || shared) return;
            disposed = true;
            db.Dispose();
        }

        public byte[]? TryGet(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            return db.Get(key ?? Array.Empty<byte>(), columnFamily);
        }

        public bool Contains(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            using var slice = db.GetSlice(key, columnFamily);
            return slice.Valid;
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            return Seek(key, direction, db, columnFamily);
        }

        public static IEnumerable<(byte[] Key, byte[] Value)> Seek(
            byte[]? prefix, SeekDirection direction, RocksDb db,
            ColumnFamilyHandle columnFamily, ReadOptions? readOptions = null)
        {
            prefix ??= Array.Empty<byte>();
            readOptions ??= RocksDbUtility.DefaultReadOptions;
            var forward = direction == SeekDirection.Forward;

            using var iterator = db.NewIterator(columnFamily, readOptions);

            _ = forward ? iterator.Seek(prefix) : iterator.SeekForPrev(prefix);
            while (iterator.Valid())
            {
                yield return (iterator.Key(), iterator.Value());
                _ = forward ? iterator.Next() : iterator.Prev();
            }
        }

        public void Put(byte[]? key, byte[]? value)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly) throw new InvalidOperationException("read only");
            db.Put(key ?? Array.Empty<byte>(), value, columnFamily);
        }

        public void PutSync(byte[]? key, byte[]? value)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly) throw new InvalidOperationException("read only");
            db.Put(key ?? Array.Empty<byte>(), value, columnFamily, RocksDbUtility.WriteSyncOptions);
        }

        public void Delete(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly) throw new InvalidOperationException("read only");
            db.Remove(key ?? Array.Empty<byte>(), columnFamily);
        }

        public ISnapshot GetSnapshot()
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly) throw new InvalidOperationException("read only");
            return new Snapshot(db, columnFamily);
        }
    }
}