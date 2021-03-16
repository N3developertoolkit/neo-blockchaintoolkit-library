using System;
using System.Collections.Generic;
using Neo.Persistence;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class RocksDbStorageProvider
    {
        class Snapshot : ISnapshot
        {
            readonly RocksDb db;
            readonly ColumnFamilyHandle columnFamily;
            readonly RocksDbSharp.Snapshot snapshot;
            readonly ReadOptions readOptions;
            readonly WriteBatch writeBatch;

            public Snapshot(RocksDb db, ColumnFamilyHandle columnFamily)
            {
                this.db = db;
                this.columnFamily = columnFamily;
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
                => db.Get(key ?? Array.Empty<byte>(), columnFamily, readOptions);

            public bool Contains(byte[]? key) => TryGet(key) != null;

            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
                => RocksDbStorageProvider.Seek(db, columnFamily, key, direction, readOptions);

            public void Put(byte[]? key, byte[] value)
                => writeBatch.Put(key ?? Array.Empty<byte>(), value, columnFamily);

            public void Delete(byte[]? key)
                => writeBatch.Delete(key ?? Array.Empty<byte>(), columnFamily);

            public void Commit() => db.Write(writeBatch);
        }
    }
}