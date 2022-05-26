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
                key ??= Array.Empty<byte>();
                // db doesn't have a putv option like write batch, so no choice but to copy value to new array w/ prefix
                Span<byte> span = stackalloc byte[value.Length + 1];
                span[0] = UPDATED_KEY;
                value.CopyTo(span.Slice(1));

                writeBatch.Put(key.AsSpan(), span, columnFamily);

                // TODO: make Putv work

                // using var prefixOwner = MemoryPool<byte>.Shared.Rent(1);
                // prefixOwner.Memory.Span[0] = PersistentTrackingStore.UPDATED_KEY;

                // using var romOwner = MemoryPool<ReadOnlyMemory<byte>>.Shared.Rent(3);
                // var keys = romOwner.Memory.Slice(0, 1);
                // keys.Span[0] = key.AsMemory();
                // var values = romOwner.Memory.Slice(1, 2);
                // values.Span[0] = prefixOwner.Memory.Slice(0, 1);
                // values.Span[1] = value.AsMemory();

                // writeBatch.PutV(keys.Span, values.Span, columnFamily);
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