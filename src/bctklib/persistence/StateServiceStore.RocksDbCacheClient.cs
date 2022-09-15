using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DotNext.Buffers;
using DotNext.IO;
using Neo.IO;
using Neo.Network.RPC;
using Neo.SmartContract.Native;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore
    {
        internal class RocksDbCacheClient : ICachingClient
        {
            static ColumnFamilyHandle GetOrCreateColumnFamily(RocksDb db, string familyName, ColumnFamilyOptions? options = null)
            {
                if (!db.TryGetColumnFamily(familyName, out var familyHandle))
                {
                    familyHandle = db.CreateColumnFamily(options ?? new ColumnFamilyOptions(), familyName);
                }
                return familyHandle;
            }

            readonly RpcClient rpcClient;
            readonly UInt256 rootHash;
            readonly RocksDb db;

            public RocksDbCacheClient(RpcClient rpcClient, UInt256 rootHash, string cachePath)
            {
                this.rpcClient = rpcClient;
                this.rootHash = rootHash;

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

            public void Dispose()
            {
                db.Dispose();
            }

            public FoundStates FindStates(UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default)
            {
                var dbKey = new ArrayBufferWriter<byte>(UInt160.Length + prefix.Length + from.Length);
                {
                    using var writer = new BinaryWriter(dbKey.AsStream());
                    scriptHash.Serialize(writer);
                    writer.Write(prefix.Span);
                    writer.Write(from.Span);
                    writer.Flush();
                }

                var family = GetOrCreateColumnFamily(db, $"{nameof(GetContractState)}");
                using (var slice = db.GetSlice(dbKey.WrittenSpan, family))
                {
                    if (slice.Valid)
                    {
                        var reader = new SpanReader<byte>(slice.GetValue());
                        var count = reader.ReadVarInt();
                        var states = new List<(byte[], byte[])>();
                        for (ulong i = 0; i < count; i++)
                        {
                            var key = reader.ReadVarBytes();
                            var value = reader.ReadVarBytes();
                            states.Add((key, value));
                        }
                        var truncated = reader.ReadBoolean();
                        Debug.Assert(reader.RemainingSpan.Length == 0);
                        return new FoundStates(states, truncated);
                    }
                }

                var found = FindProvenStates(rpcClient, rootHash, scriptHash, prefix.Span, from.Span);
                var valueSeq = new Nerdbank.Streams.Sequence<byte>();
                {
                    using var writer = new BinaryWriter(valueSeq.AsStream());
                    writer.WriteVarInt(found.States.Count);
                    for (int i = 0; i < found.States.Count; i++)
                    {
                        var (key, value) = found.States[i];
                        writer.WriteVarBytes(key);
                        writer.WriteVarBytes(value);
                    }
                    writer.Write(found.Truncated);
                    writer.Flush();
                }

                using var batch = new WriteBatch();
                batch.PutVector(dbKey.WrittenMemory, valueSeq.AsReadOnlySequence, family);
                db.Write(batch);

                return found;
            }

            const byte NON_NULL_PREFIX = 1;
            const byte NULL_PREFIX = 0;
            readonly static ReadOnlyMemory<byte> nullPrefix = (new byte[] { NULL_PREFIX }).AsMemory();
            readonly static ReadOnlyMemory<byte> notNullPrefix = (new byte[] { NON_NULL_PREFIX }).AsMemory();

            public byte[]? GetContractState(UInt160 scriptHash, ReadOnlyMemory<byte> key)
            {
                var dbKey = new ArrayBufferWriter<byte>(UInt160.Length + key.Length);
                {
                    using var writer = new BinaryWriter(dbKey.AsStream());
                    scriptHash.Serialize(writer);
                    writer.Write(key.Span);
                    writer.Flush();
                }

                var family = GetOrCreateColumnFamily(db, $"{nameof(GetContractState)}");
                using (var slice = db.GetSlice(dbKey.WrittenSpan, family))
                {
                    if (slice.Valid)
                    {
                        var span = slice.GetValue();
                        if (span.Length == 1 && span[0] == NULL_PREFIX) return null;
                        return span[1..].ToArray();
                    }
                }

                var state = GetProvenState(rpcClient, rootHash, scriptHash, key.Span);
                if (state is null)
                {
                    db.Put(dbKey.WrittenSpan, nullPrefix.Span, family);
                }
                else
                {
                    using var batch = new WriteBatch();
                    batch.PutVector(family, dbKey.WrittenMemory, notNullPrefix, state.AsMemory());
                    db.Write(batch);
                }
                return state;
            }

            public byte[] GetLedgerStorage(ReadOnlyMemory<byte> key)
            {
                var family = GetOrCreateColumnFamily(db, $"{nameof(GetLedgerStorage)}");
                using (var slice = db.GetSlice(key.Span, family))
                {
                    if (slice.Valid)
                    {
                        return slice.GetValue().ToArray();
                    }
                }

                var storage = rpcClient.GetStorage(NativeContract.Ledger.Hash, key.Span);
                db.Put(key.Span, storage, family);
                return storage;
            }
        }
    }
}
