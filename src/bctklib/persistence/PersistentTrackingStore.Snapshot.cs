using System;
using System.Buffers;
using System.Collections.Generic;
using Neo.Persistence;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class PersistentTrackingStore
    {
        class Snapshot : ISnapshot
        {
            readonly RocksDb db;
            readonly ColumnFamilyHandle columnFamily;
            readonly IReadOnlyStore store;
            readonly RocksDbSharp.Snapshot snapshot;
            readonly ReadOptions readOptions;
            readonly WriteBatch writeBatch;

            public Snapshot(RocksDb db, ColumnFamilyHandle columnFamily, IReadOnlyStore store)
            {
                this.db = db;
                this.columnFamily = columnFamily;
                this.store = store;

                snapshot = db.CreateSnapshot();
                readOptions = new ReadOptions()
                    .SetSnapshot(snapshot)
                    .SetFillCache(false);
                writeBatch = new WriteBatch();
            }

            public void Dispose()
            {
                snapshot.Dispose();
                writeBatch.Dispose();
            }

            public byte[]? TryGet(byte[]? key)
            {
                if (snapshot.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(Snapshot));
                return PersistentTrackingStore.TryGet(key, db, columnFamily, readOptions, store);
            }

            public bool Contains(byte[]? key)
            {
                if (snapshot.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(Snapshot));
                return PersistentTrackingStore.Contains(key, db, columnFamily, readOptions, store);
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
            {
                if (snapshot.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(Snapshot));
                return PersistentTrackingStore.Seek(key, direction, db, columnFamily, readOptions, store);
            }

            public unsafe void Put(byte[]? key, byte[] value)
            {
                if (snapshot.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(Snapshot));
                if (value is null) throw new NullReferenceException(nameof(value));

                key ??= Array.Empty<byte>();
                var pool = ArrayPool<byte>.Shared;
                var prefix = pool.Rent(1);
                try
                {
                    prefix[0] = UPDATED_KEY;
                    writeBatch.PutVector(columnFamily, key, prefix.AsMemory(0, 1), value);
                }
                finally
                {
                    pool.Return(prefix);
                }
            }

            public void Delete(byte[]? key)
            {
                if (snapshot.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(Snapshot));
                key ??= Array.Empty<byte>();
                if (store.Contains(key))
                {
                    Span<byte> value = stackalloc byte[] { PersistentTrackingStore.DELETED_KEY };
                    writeBatch.Put(key.AsSpan(), value, columnFamily);
                }
                else
                {
                    writeBatch.Delete(key, columnFamily);
                }
            }

            public void Commit()
            {
                if (snapshot.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(Snapshot));
                db.Write(writeBatch);
            }
        }
    }
}