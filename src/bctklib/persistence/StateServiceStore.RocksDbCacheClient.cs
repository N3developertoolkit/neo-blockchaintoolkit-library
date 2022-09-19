using System;
using System.Collections.Generic;
using System.Linq;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore
    {
        internal sealed class RocksDbCacheClient : ICacheClient
        {
            readonly RocksDb db;
            readonly bool shared;
            bool disposed;

            public RocksDbCacheClient(RocksDb db, bool shared)
            {
                this.db = db;
                this.shared = shared;
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    if (!shared) db.Dispose();
                    disposed = true;
                }
            }

            static string GetFamilyName(UInt160 contractHash, byte? prefix)
            {
                return prefix.HasValue
                    ? $"{contractHash}{prefix.Value}"
                    : $"{contractHash}";
            }

            static string GetFamilyName(UInt160 contractHash) => $"G{contractHash}";

            const byte NULL_PREFIX = 0;
            readonly static ReadOnlyMemory<byte> nullPrefix = (new byte[] { NULL_PREFIX }).AsMemory();
            readonly static ReadOnlyMemory<byte> notNullPrefix = (new byte[] { NULL_PREFIX + 1 }).AsMemory();

            public bool TryGetCachedStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[]? value)
            {
                if (disposed) throw new ObjectDisposedException(nameof(RocksDbCacheClient));

                var familyName = GetFamilyName(contractHash);
                if (db.TryGetColumnFamily(familyName, out var columnFamily))
                {
                    using var slice = db.GetSlice(key.Span, columnFamily);
                    if (slice.Valid)
                    {
                        var span = slice.GetValue();
                        value = span.Length != 1 || span[0] != NULL_PREFIX
                            ? span[1..].ToArray()
                            : null;
                        return true;
                    }
                }

                value = null;
                return false;
            }

            public void CacheStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, byte[]? value)
            {
                if (disposed) throw new ObjectDisposedException(nameof(RocksDbCacheClient));

                var familyName = GetFamilyName(contractHash);
                var columnFamily = RocksDbUtility.GetOrCreateColumnFamily(db, familyName);

                if (value is null)
                {
                    db.Put(key.Span, nullPrefix.Span, columnFamily);
                }
                else
                {
                    using var batch = new WriteBatch();
                    batch.PutVector(columnFamily, key, notNullPrefix, value.AsMemory());
                    db.Write(batch);
                }
            }

            public bool TryGetCachedFoundStates(UInt160 contractHash, byte? prefix, out IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> value)
            {
                if (disposed) throw new ObjectDisposedException(nameof(RocksDbCacheClient));

                var familyName = GetFamilyName(contractHash, prefix);
                if (db.TryGetColumnFamily(familyName, out var columnFamily))
                {
                    value = GetCachedFoundStates(columnFamily);
                    return true;
                }

                value = Enumerable.Empty<(ReadOnlyMemory<byte>, byte[])>();
                return false;

                IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> GetCachedFoundStates(ColumnFamilyHandle columnFamily)
                {
                    using var iterator = db.NewIterator(columnFamily);
                    iterator.Seek(default(ReadOnlySpan<byte>));
                    while (iterator.Valid())
                    {
                        yield return (iterator.Key(), iterator.Value());
                        iterator.Next();
                    }
                }
            }

            public ICacheSnapshot GetFoundStatesSnapshot(UInt160 contractHash, byte? prefix)
            {
                if (disposed) throw new ObjectDisposedException(nameof(RocksDbCacheClient));
                var familyName = GetFamilyName(contractHash, prefix);
                var columnFamily = RocksDbUtility.GetOrCreateColumnFamily(db, familyName);
                return new Snapshot(db, columnFamily);
            }

            public void DropCachedFoundStates(UInt160 contractHash, byte? prefix)
            {
                if (disposed) throw new ObjectDisposedException(nameof(RocksDbCacheClient));

                var familyName = GetFamilyName(contractHash, prefix);
                db.DropColumnFamily(familyName);
            }


            class Snapshot : ICacheSnapshot
            {
                readonly RocksDb db;
                readonly ColumnFamilyHandle columnFamily;
                readonly WriteBatch writeBatch = new();

                public Snapshot(RocksDb db, ColumnFamilyHandle columnFamily)
                {
                    this.db = db;
                    this.columnFamily = columnFamily;
                }

                public void Dispose()
                {
                    writeBatch.Dispose();
                }

                public void Add(ReadOnlyMemory<byte> key, byte[] value)
                {
                    writeBatch.Put(key.Span, value.AsSpan(), columnFamily);
                }

                public void Commit()
                {
                    db.Write(writeBatch);
                }
            }
        }
    }
}
