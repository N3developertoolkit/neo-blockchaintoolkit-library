using System;
using System.Collections.Generic;
using System.Linq;
using Neo.Persistence;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class PersistentTrackingStore : IStore
    {
        const byte UPDATED_KEY = 1;
        const byte DELETED_KEY = 0;

        readonly RocksDb db;
        readonly ColumnFamilyHandle columnFamily;
        readonly IReadOnlyStore store;
        readonly bool shared;
        bool disposed;

        public PersistentTrackingStore(RocksDb db, IReadOnlyStore store, bool shared = false)
            : this(db, db.GetDefaultColumnFamily(), store, false)
        {
        }

        public PersistentTrackingStore(RocksDb db, string columnFamilyName, IReadOnlyStore store, bool shared = false)
            : this(db, db.GetColumnFamilyHandle(columnFamilyName), store, false)
        {
        }

        public PersistentTrackingStore(RocksDb db, ColumnFamilyHandle columnFamily, IReadOnlyStore store, bool shared = false)
        {
            this.db = db;
            this.columnFamily = columnFamily;
            this.store = store;
            this.shared = shared;
        }

        public void Dispose()
        {
            if (shared || disposed) return;
            disposed = true;
            db.Dispose();
            if (store is IDisposable disposable) disposable.Dispose();
        }

        public byte[]? TryGet(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            return TryGet(key, db, columnFamily, null, store);
        }

        static byte[]? TryGet(byte[]? key, RocksDb db, ColumnFamilyHandle columnFamily, ReadOptions? readOptions, IReadOnlyStore store)
        {
            using var slice = db.GetSlice(key, columnFamily, readOptions);
            if (!slice.Valid) return store.TryGet(key);
            var value = slice.GetValue();
            if (value[0] == DELETED_KEY) return null;
            if (value[0] != UPDATED_KEY) throw new Exception("value must have 0 or 1 prefix");
            return value.Slice(1).ToArray();
        }

        public bool Contains(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            return Contains(key, db, columnFamily, null, store);
        }

        static bool Contains(byte[]? key, RocksDb db, ColumnFamilyHandle columnFamily, ReadOptions? readOptions, IReadOnlyStore store)
        {
            using var slice = db.GetSlice(key, columnFamily, readOptions);
            if (slice.Valid)
            {
                var value = slice.GetValue();
                return value[0] == UPDATED_KEY;
            }
            else
            {
                return store.Contains(key);
            }
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            return Seek(key, direction, db, columnFamily, null, store);
        }

        public static IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction, RocksDb db, ColumnFamilyHandle columnFamily, ReadOptions? readOptions, IReadOnlyStore store)
        {
            var trackedItems = SeekTracked(key, direction, db, columnFamily);
            var storeItems = store.Seek(key, direction).Where(KeyUntracked);

            var comparer = direction == SeekDirection.Forward
                ? ReadOnlyMemoryComparer.Default
                : ReadOnlyMemoryComparer.Reverse;

            return trackedItems.Concat(storeItems).OrderBy(kvp => kvp.Key, comparer);

            static IEnumerable<(byte[] Key, byte[] Value)> SeekTracked(
                byte[]? key, SeekDirection direction, RocksDb db,
                ColumnFamilyHandle columnFamily, ReadOptions? readOptions = null)
            {
                key ??= Array.Empty<byte>();
                readOptions ??= RocksDbUtility.DefaultReadOptions;
                var forward = direction == SeekDirection.Forward;

                using var iterator = db.NewIterator(columnFamily, readOptions);

                _ = forward ? iterator.Seek(key) : iterator.SeekForPrev(key);
                while (iterator.Valid())
                {
                    var value = iterator.GetValueSpan();
                    if (value[0] == UPDATED_KEY)
                    {
                        yield return (iterator.Key(), value.Slice(1).ToArray());
                    }
                    else
                    {
                        if (value[0] != DELETED_KEY) throw new Exception("value must have 0 or 1 prefix");
                    }
                    _ = forward ? iterator.Next() : iterator.Prev();
                }
            }

            bool KeyUntracked((byte[] Key, byte[] Value) kvp)
            {
                using var slice = db.GetSlice(kvp.Key, columnFamily, readOptions);
                return !slice.Valid;
            }
        }

        public void Put(byte[]? key, byte[] value)
        {
            Put(key, value, null);
        }

        public void PutSync(byte[]? key, byte[] value)
        {
            Put(key, value, RocksDbUtility.WriteSyncOptions);
        }

        void Put(byte[]? key, byte[] value, WriteOptions? writeOptions)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));

            key ??= Array.Empty<byte>();
            // db doesn't have a putv option like write batch, so no choice but to copy value to new array w/ prefix
            Span<byte> span = stackalloc byte[value.Length + 1];
            span[0] = UPDATED_KEY;
            value.CopyTo(span.Slice(1));
            db.Put(key.AsSpan(), span, columnFamily, writeOptions);
        }

        public void Delete(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
            key ??= Array.Empty<byte>();
            if (store.Contains(key))
            {
                Span<byte> value = stackalloc byte[] { DELETED_KEY };
                db.Put(key.AsSpan(), value, columnFamily);
            }
            else
            {
                db.Remove(key);
            }
        }

        public ISnapshot GetSnapshot()
        {
            return new Snapshot(db, columnFamily, store);
        }
    }
}