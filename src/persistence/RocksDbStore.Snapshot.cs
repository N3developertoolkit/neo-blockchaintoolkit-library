using System;
using System.Collections.Generic;
using Neo.IO.Caching;
using Neo.Persistence;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class RocksDbStore
    {
        class Snapshot : ISnapshot
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

            byte[] IReadOnlyStore.TryGet(byte table, byte[]? key)
                => store.db.Get(key ?? Array.Empty<byte>(), store.GetColumnFamily(table), readOptions);

            bool IReadOnlyStore.Contains(byte table, byte[] key)
                => null != store.db.Get(key ?? Array.Empty<byte>(), store.GetColumnFamily(table), readOptions);

            IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte table, byte[]? key, SeekDirection direction)
                => RocksDbStore.Seek(store.db, key, store.GetColumnFamily(table), direction, readOptions);

            void ISnapshot.Commit() => store.db.Write(writeBatch);

            void ISnapshot.Put(byte table, byte[]? key, byte[] value)
                => writeBatch.Put(key ?? Array.Empty<byte>(), value, store.GetColumnFamily(table));

            void ISnapshot.Delete(byte table, byte[]? key)
                => writeBatch.Delete(key ?? Array.Empty<byte>(), store.GetColumnFamily(table));
        }
    }
}
