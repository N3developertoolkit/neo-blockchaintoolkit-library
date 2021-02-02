using System;
using System.Collections.Generic;
using Neo.IO.Caching;
using Neo.Persistence;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class RocksDbStore
    {
        class Snapshot : ISnapshot, IExpressSnapshot
        {
            private readonly RocksDbStore store;
            private readonly RocksDbSharp.Snapshot snapshot;
            private readonly ReadOptions readOptions;
            private readonly WriteBatch writeBatch;

            public Snapshot(RocksDbStore store)
            {
                this.store = store;
                snapshot = store.db.CreateSnapshot();
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

            public byte[]? TryGet(byte table, byte[]? key)
                => store.db.Get(key ?? Array.Empty<byte>(), store.GetColumnFamily(table), readOptions);

            public bool Contains(byte table, byte[]? key) => TryGet(table, key) != null;

            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[]? key, SeekDirection direction)
                => RocksDbStore.Seek(store.db, key, store.GetColumnFamily(table), direction, readOptions);

            public void Put(byte table, byte[]? key, byte[] value)
                => writeBatch.Put(key ?? Array.Empty<byte>(), value, store.GetColumnFamily(table));

            public void Delete(byte table, byte[]? key)
                => writeBatch.Delete(key ?? Array.Empty<byte>(), store.GetColumnFamily(table));

            public void Commit() => store.db.Write(writeBatch);

            byte[]? IReadOnlyStore.TryGet(byte[]? key) => TryGet(default, key);
            bool IReadOnlyStore.Contains(byte[] key) => Contains(default, key);
            IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte[]? key, SeekDirection direction) => Seek(default, key, direction);
            void ISnapshot.Put(byte[]? key, byte[] value) => Put(default, key, value);
            void ISnapshot.Delete(byte[]? key) => Delete(default, key);
        }
    }
}