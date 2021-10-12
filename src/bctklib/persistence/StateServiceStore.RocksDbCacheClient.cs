using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using MessagePack;
using MessagePack.Formatters;
using Neo.IO.Json;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore
    {
        internal class RocksDbCacheClient : ICachingClient
        {
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
                var family = db.GetOrCreateColumnFamily(familyName);

                var keyLength = prefix.Length + from.Length + (count.HasValue ? sizeof(int) : 0);
                Span<byte> key = stackalloc byte[keyLength];
                prefix.Span.CopyTo(key);
                if (from.Length > 0) from.Span.CopyTo(key.Slice(prefix.Length));
                if (count.HasValue) BinaryPrimitives.WriteInt32LittleEndian(key.Slice(prefix.Length + from.Length), count.Value);

                var json = GetCachedJson(key, family, () =>
                {
                    var @params = StateAPI.MakeFindStatesParams(rootHash, scriptHash, prefix.Span, from.Span, count);
                    return rpcClient.RpcSend("findstates", @params);
                });
                return RpcFoundStates.FromJson(json);
            }

            public UInt256 GetBlockHash(uint index)
            {
                var family = db.GetDefaultColumnFamily();
                var json = GetCachedJson($"{nameof(GetBlockHash)}{index}", family,
                    () => rpcClient.RpcSend("getblockhash", index));
                return UInt256.Parse(json.AsString());
            }

            static readonly IMessagePackFormatter<byte[]?> byteArrayFormatter = 
                MessagePackSerializerOptions.Standard.Resolver.GetFormatter<byte[]?>();

            public byte[]? GetState(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> key)
            {
                var familyName = $"{nameof(GetState)}{rootHash}{scriptHash}";
                var family = db.GetOrCreateColumnFamily(familyName);
                var cachedState = db.Get(key.Span, family);

                if (cachedState != null)
                {
                    var reader = new MessagePackReader(cachedState);
                    return byteArrayFormatter.Deserialize(ref reader, MessagePackSerializerOptions.Standard);
                }
                else
                {
                    var state = rpcClient.GetState(rootHash, scriptHash, key.Span);

                    var buffer = new ArrayBufferWriter<byte>();
                    var writer = new MessagePackWriter(buffer);
                    writer.Write(state);
                    writer.Flush();
                    db.Put(key.Span, buffer.WrittenSpan, family);

                    return state;
                }
            }

            public RpcStateRoot GetStateRoot(uint index)
            {
                var family = db.GetDefaultColumnFamily();
                var json = GetCachedJson($"{nameof(GetStateRoot)}{index}", family,
                    () => rpcClient.RpcSend("getstateroot", index));
                return RpcStateRoot.FromJson(json);
            }

            public byte[] GetLedgerStorage(ReadOnlyMemory<byte> key)
            {
                var contractHash = Neo.SmartContract.Native.NativeContract.Ledger.Hash;

                var familyName = $"{nameof(GetLedgerStorage)}";
                var family = db.GetOrCreateColumnFamily(familyName);
                var json = GetCachedJson(key.Span, family,
                    () => rpcClient.RpcSend("getstorage", contractHash.ToString(), Convert.ToBase64String(key.Span)));
                return Convert.FromBase64String(json.AsString());
            }

            readonly static Encoding encoding = Encoding.UTF8;
            IMemoryOwner<byte> GetKeyBuffer(string text, out int count)
            {
                count = encoding.GetByteCount(text);
                var owner = MemoryPool<byte>.Shared.Rent(count);
                if (encoding.GetBytes(text, owner.Memory.Span) != count)
                    throw new InvalidOperationException();
                return owner;
            }

            JObject GetCachedJson(string key, ColumnFamilyHandle family, Func<JObject> factory)
            {
                using var keyBuffer = GetKeyBuffer(key, out var count);
                return GetCachedJson(keyBuffer.Memory.Slice(0, count).Span, family, factory);
            }

            JObject GetCachedJson(ReadOnlySpan<byte> key, ColumnFamilyHandle family, Func<JObject> factory)
            {
                var value = db.Get(key, family);
                if (value != null)
                {
                    return JObject.Parse(value);
                }
                else
                {
                    var json = factory();
                    db.Put(key, json.ToByteArray(false), family);
                    return json;
                }
            }
        }
    }
}