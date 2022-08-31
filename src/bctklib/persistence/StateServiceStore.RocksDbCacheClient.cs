using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Formatters;
using Neo.Json;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore
    {
        internal class RocksDbCacheClient : ICachingClient
        {
            readonly static ColumnFamilyOptions defaultColumnFamilyOptions = new ColumnFamilyOptions();

            static ColumnFamilyHandle GetOrCreateColumnFamily(RocksDb db, string familyName, ColumnFamilyOptions? options = null)
            {
                if (!db.TryGetColumnFamily(familyName, out var familyHandle))
                {
                    familyHandle = db.CreateColumnFamily(options ?? defaultColumnFamilyOptions, familyName);
                }
                return familyHandle;
            }

            readonly RpcClient rpcClient;
            readonly RocksDb db;

            public RocksDbCacheClient(RpcClient rpcClient, string cachePath)
            {
                this.rpcClient = rpcClient;

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

            public RpcFoundStates FindStates(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default, int? count = null)
            {
                var familyName = $"{nameof(FindStates)}{rootHash}{scriptHash}";
                var family = GetOrCreateColumnFamily(db, familyName);

                var keyLength = prefix.Length + from.Length + (count.HasValue ? sizeof(int) : 0);
                Span<byte> key = stackalloc byte[keyLength];
                prefix.Span.CopyTo(key);
                if (from.Length > 0) from.Span.CopyTo(key.Slice(prefix.Length));
                if (count.HasValue) BinaryPrimitives.WriteInt32LittleEndian(key.Slice(prefix.Length + from.Length), count.Value);

                var json = GetOrAddJson(key, family, () =>
                {
                    var @params = StateAPI.MakeFindStatesParams(rootHash, scriptHash, prefix.Span, from.Span, count);
                    return (Json.JObject)rpcClient.RpcSend("findstates", @params);
                });
                return RpcFoundStates.FromJson(json);
            }

            const byte GetBlockHash_Prefix = 0xC0;
            const byte GetStateRootAsync_Prefix = 0xC1;

            public UInt256 GetBlockHash(uint index)
            {
                var family = db.GetDefaultColumnFamily();
                Span<byte> key = stackalloc byte[1 + sizeof(uint)];
                key[0] = GetBlockHash_Prefix;
                BinaryPrimitives.WriteUInt32LittleEndian(key.Slice(1), index);

                using (var slice = db.GetSlice(key, family))
                {
                    if (slice.Valid)
                    {
                        return new UInt256(slice.GetValue());
                    }
                }

                var hash = rpcClient.GetBlockHash(index);
                db.Put(key, Neo.IO.Helper.ToArray(hash).AsSpan(), family);
                return hash;
            }

            public async Task<UInt256> GetStateRootHashAsync(uint index)
            {
                var family = db.GetDefaultColumnFamily();
                const int keyLength = 1 + sizeof(uint);
                var owner = MemoryPool<byte>.Shared.Rent(keyLength);
                var key = owner.Memory.Slice(0, keyLength);
                key.Span[0] = GetStateRootAsync_Prefix;
                BinaryPrimitives.WriteUInt32LittleEndian(key.Span.Slice(1), index);

                using (var slice = db.GetSlice(key.Span, family))
                {
                    if (slice.Valid)
                    {
                        return new UInt256(slice.GetValue());
                    }
                }

                var stateApi = new StateAPI(rpcClient);
                var stateRoot = await stateApi.GetStateRootAsync(index).ConfigureAwait(false);
                db.Put(key.Span, Neo.IO.Helper.ToArray(stateRoot.RootHash).AsSpan(), family);
                return stateRoot.RootHash;
            }

            static readonly IMessagePackFormatter<byte[]?> byteArrayFormatter =
                MessagePackSerializerOptions.Standard.Resolver.GetFormatter<byte[]?>();

            ColumnFamilyHandle GetStateColumnFamily(UInt256 rootHash, UInt160 scriptHash)
            {
                var familyName = $"{nameof(GetState)}{rootHash}{scriptHash}";
                return GetOrCreateColumnFamily(db, familyName);
            }

            // method used for testing
            internal byte[]? GetCachedState(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> key)
            {
                var family = GetStateColumnFamily(rootHash, scriptHash);
                return GetCachedState(key.Span, family);
            }

            byte[]? GetCachedState(ReadOnlySpan<byte> key, ColumnFamilyHandle family) => db.Get(key, family);

            public byte[]? GetState(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> key)
            {
                var family = GetStateColumnFamily(rootHash, scriptHash);
                var cachedState = GetCachedState(key.Span, family);

                if (cachedState != null)
                {
                    var reader = new MessagePackReader(cachedState);
                    return byteArrayFormatter.Deserialize(ref reader, MessagePackSerializerOptions.Standard);
                }
                else
                {
                    var state = rpcClient.GetProvenState(rootHash, scriptHash, key.Span);

                    var buffer = new ArrayBufferWriter<byte>();
                    var writer = new MessagePackWriter(buffer);
                    byteArrayFormatter.Serialize(ref writer, state, MessagePackSerializerOptions.Standard);
                    writer.Flush();
                    db.Put(key.Span, buffer.WrittenSpan, family);

                    return state;
                }
            }

            public byte[] GetLedgerStorage(ReadOnlyMemory<byte> key)
            {
                var contractHash = Neo.SmartContract.Native.NativeContract.Ledger.Hash;

                var familyName = $"{nameof(GetLedgerStorage)}";
                var family = GetOrCreateColumnFamily(db, familyName);
                var json = GetOrAddJson(key.Span, family,
                    () => (Json.JObject)rpcClient.RpcSend("getstorage", contractHash.ToString(), Convert.ToBase64String(key.Span)));
                return Convert.FromBase64String(json.AsString());
            }

            JObject GetOrAddJson(ReadOnlySpan<byte> key, ColumnFamilyHandle family, Func<JObject> factory)
            {
                using (var slice = db.GetSlice(key, family))
                {
                    if (slice.Valid)
                    {
                        var token = JToken.Parse(slice.GetValue());
                        if (token != null && token is JObject jObject)
                        {
                            return jObject;
                        }
                    }
                }

                var json = factory();
                var buffer = json.ToByteArray(false);
                db.Put(key, buffer.AsSpan(), family);
                return json;
            }
        }
    }
}