using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo;
using Neo.IO;
using Neo.Network.RPC;
using Neo.Persistence;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore
    {
        class RocksDbCache : ICache
        {
            readonly RocksDb db;
            readonly Func<UInt160, IEnumerable<(byte[] key, byte[] value)>> funcEnumStates;

            public RocksDbCache(Func<UInt160, IEnumerable<(byte[] key, byte[] value)>> funcEnumStates, string cachePath)
            {
                this.funcEnumStates = funcEnumStates;

                if (!Directory.Exists(cachePath)) Directory.CreateDirectory(cachePath);
                var columnFamilies = GetColumnFamilies(cachePath);
                db = RocksDb.Open(new DbOptions().SetCreateIfMissing(true), cachePath, columnFamilies);

                static ColumnFamilies GetColumnFamilies(string path)
                {
                    if (RocksDb.TryListColumnFamilies(new DbOptions(), path, out var names))
                    {
                        var columnFamilyOptions = new ColumnFamilyOptions();
                        var families = new ColumnFamilies();
                        foreach (var name in names)
                        {
                            families.Add(name, columnFamilyOptions);
                        }
                        return families;
                    }

                    return new ColumnFamilies();
                }
            }

            public IEnumerable<(byte[] key, byte[] value)> Seek(UInt160 contractHash, ReadOnlyMemory<byte> prefix, SeekDirection direction)
            {
                var family = PopulateLocalCache(contractHash);
                using var iterator = db.NewIterator(family);

                if (direction == SeekDirection.Forward)
                {
                    Seek(iterator, prefix.Span);
                    while (iterator.Valid())
                    {
                        yield return (iterator.Key(), iterator.Value());
                        iterator.Next();
                    }
                }
                else
                {
                    SeekForPrev(iterator, prefix.Span);
                    while (iterator.Valid())
                    {
                        yield return (iterator.Key(), iterator.Value());
                        iterator.Prev();
                    }
                }

                unsafe static Iterator Seek(Iterator @this, ReadOnlySpan<byte> prefix)
                {
                    fixed (byte* prefixPtr = prefix)
                    {
                        return @this.Seek(prefixPtr, (ulong)prefix.Length);
                    }
                }

                unsafe static Iterator SeekForPrev(Iterator @this, ReadOnlySpan<byte> prefix)
                {
                    fixed (byte* prefixPtr = prefix)
                    {
                        return @this.SeekForPrev(prefixPtr, (ulong)prefix.Length);
                    }
                }
            }

            static readonly ReadOptions defaultReadOptions = new ReadOptions();

            public bool TryGet(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[] value)
            {
                var family = PopulateLocalCache(contractHash);
                return TryGet(db, family, key.Span, defaultReadOptions, out value);

                static unsafe bool TryGet(RocksDb db, ColumnFamilyHandle columnFamily, ReadOnlySpan<byte> key, ReadOptions readOptions, out byte[] value)
                {
                    fixed (byte* keyPtr = key)
                    {
                        var pinnableSlice = Native.Instance.rocksdb_get_pinned_cf(db.Handle, readOptions.Handle,
                            columnFamily.Handle, (IntPtr)keyPtr, (UIntPtr)key.Length);

                        try
                        {
                            var valuePtr = Native.Instance.rocksdb_pinnableslice_value(pinnableSlice, out var valueLength);
                            if (valuePtr == IntPtr.Zero)
                            {
                                value = Array.Empty<byte>();
                                return false;
                            }

                            var span = new ReadOnlySpan<byte>((byte*)valuePtr, (int)valueLength);
                            value = span.ToArray();
                            return true;
                        }
                        finally
                        {
                            Native.Instance.rocksdb_pinnableslice_destroy(pinnableSlice);
                        }
                    }
                }
            }

            ColumnFamilyHandle PopulateLocalCache(UInt160 contractHash)
            {
                var family = GetColumnFamily(contractHash);
                var contractHashArray = contractHash.ToArray();
                if (db.Get(contractHashArray) == null)
                {
                    using var writeBatch = new WriteBatch();
                    writeBatch.Put(contractHashArray, Array.Empty<byte>());
                    foreach (var kvp in funcEnumStates(contractHash))
                    {
                        writeBatch.Put(kvp.key, kvp.value, family);
                    }
                    db.Write(writeBatch);
                }
                return family;
            }

            static readonly ColumnFamilyOptions defaultColumnFamilyOptions = new ColumnFamilyOptions();

            ColumnFamilyHandle GetColumnFamily(Neo.UInt160 contract)
            {
                if (contract == null) return db.GetDefaultColumnFamily();

                var name = $"{contract}";
                if (db.TryGetColumnFamily(name, out var columnFamily)) return columnFamily;

                return db.CreateColumnFamily(defaultColumnFamilyOptions, name);
            }
        }
    }
}