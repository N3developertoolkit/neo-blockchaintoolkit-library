using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Neo.BlockchainToolkit.Persistence.RPC;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using RocksDbSharp;
using RpcVersion = Neo.BlockchainToolkit.Persistence.RPC.RpcVersion;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore
    {
        class RocksDbCacheClient : ICachingClient
        {
            readonly SyncRpcClient rpcClient;
            readonly RocksDb db;

            public RocksDbCacheClient(SyncRpcClient rpcClient, string cachePath)
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

                var json = GetCachedJson(key, family, () => {
                    var @params = RpcClientExtensions.MakeFindStatesParams(rootHash, scriptHash, prefix.Span, from.Span, count);
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

            public byte[]? GetState(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> key)
            {
                var familyName = $"{nameof(GetState)}{rootHash}{scriptHash}";
                var family = db.GetOrCreateColumnFamily(familyName);
                var value = db.Get(key.Span, family);

                RpcResponse rpcResponse;
                if (value != null)
                {
                    var json = JObject.Parse(value);
                    rpcResponse = RpcResponse.FromJson(json);
                }
                else
                {
                    var request = SyncRpcClient.AsRpcRequest("getstate",
                        rootHash.ToString(), scriptHash.ToString(), Convert.ToBase64String(key.Span));
                    rpcResponse = rpcClient.Send(request);
                    db.Put(key.Span, rpcResponse.ToJson().ToByteArray(false), family);
                }

                return rpcResponse.AsStateResponse();
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

            public RpcVersion GetVersion()
            {
                var family = db.GetDefaultColumnFamily();
                var key = Encoding.UTF8.GetBytes(nameof(GetVersion));
                var json = GetCachedJson(key, family, () => rpcClient.RpcSend("getversion"));
                return RpcVersion.FromJson(json);
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