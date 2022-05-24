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
            return db.Get(key ?? Array.Empty<byte>(), columnFamily);
        }

        public bool Contains(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            return TryGet(key) != null;
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            return Seek(key, direction, db, columnFamily);
        }

        static IEnumerable<(byte[] key, byte[] value)> Seek(
            ReadOnlySpan<byte> prefix, SeekDirection direction, RocksDb db, 
            ColumnFamilyHandle columnFamily, ReadOptions? readOptions = null
        ) {
            var iterator = db.NewIterator(columnFamily, readOptions);

            if (direction == SeekDirection.Forward)
            {
                iterator.Seek(prefix);
                return SeekInternal(iterator, iterator.Next);
            }
            else
            {
                iterator.SeekForPrev(prefix);
                return SeekInternal(iterator, iterator.Prev);
            }

            IEnumerable<(byte[] key, byte[] value)> SeekInternal(Iterator iterator, Func<Iterator> nextAction)
            {
                using (iterator)
                {
                    while (iterator.Valid())
                    {
                        yield return (iterator.Key(), iterator.Value());
                        nextAction();
                    }
                }
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
            db.Put(key ?? Array.Empty<byte>(), value, columnFamily, writeSyncOptions);
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