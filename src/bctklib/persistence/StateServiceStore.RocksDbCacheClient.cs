// using System;
// using System.Buffers;
// using System.Buffers.Binary;
// using System.IO;
// using System.Threading.Tasks;
// using MessagePack;
// using MessagePack.Formatters;
// using Neo.IO;
// using Neo.Json;
// using Neo.Network.RPC;
// using Neo.Network.RPC.Models;
// using RocksDbSharp;

// namespace Neo.BlockchainToolkit.Persistence
// {
//     public partial class StateServiceStore
//     {
//         internal class RocksDbCacheClient : ICachingClient
//         {
//             const byte GetBlockHash_Prefix = 0xC0;
//             const byte GetStateRootAsync_Prefix = 0xC1;
 
//             readonly static ColumnFamilyOptions defaultColumnFamilyOptions = new ColumnFamilyOptions();

//             static ColumnFamilyHandle GetOrCreateColumnFamily(RocksDb db, string familyName, ColumnFamilyOptions? options = null)
//             {
//                 if (!db.TryGetColumnFamily(familyName, out var familyHandle))
//                 {
//                     familyHandle = db.CreateColumnFamily(options ?? defaultColumnFamilyOptions, familyName);
//                 }
//                 return familyHandle;
//             }

//             readonly RpcClient rpcClient;
//             readonly RocksDb db;

//             public RocksDbCacheClient(RpcClient rpcClient, string cachePath)
//             {
//                 this.rpcClient = rpcClient;

//                 if (!Directory.Exists(cachePath)) Directory.CreateDirectory(cachePath);
//                 db = RocksDbUtility.OpenDb(cachePath);
//             }

//             public void Dispose()
//             {
//                 db.Dispose();
//             }

//             public async Task<UInt256> GetStateRootHashAsync(uint index)
//             {
//                 var family = db.GetDefaultColumnFamily();

//                 const int keyLength = 1 + sizeof(uint);
//                 var owner = MemoryPool<byte>.Shared.Rent(keyLength);
//                 var key = owner.Memory.Slice(0, keyLength);
//                 key.Span[0] = GetStateRootAsync_Prefix;
//                 BinaryPrimitives.WriteUInt32LittleEndian(key.Span.Slice(1), index);

//                 using (var slice = db.GetSlice(key.Span, family))
//                 {
//                     if (slice.Valid)
//                     {
//                         return new UInt256(slice.GetValue());
//                     }
//                 }

//                 var stateApi = new StateAPI(rpcClient);
//                 var stateRoot = await stateApi.GetStateRootAsync(index).ConfigureAwait(false);
//                 db.Put(key.Span, stateRoot.RootHash.ToArray().AsSpan(), family);
//                 return stateRoot.RootHash;
//             }

//             public UInt256 GetBlockHash(uint index)
//             {
//                 var family = db.GetDefaultColumnFamily();
//                 Span<byte> key = stackalloc byte[1 + sizeof(uint)];
//                 key[0] = GetBlockHash_Prefix;
//                 BinaryPrimitives.WriteUInt32LittleEndian(key.Slice(1), index);

//                 using (var slice = db.GetSlice(key, family))
//                 {
//                     if (slice.Valid)
//                     {
//                         return new UInt256(slice.GetValue());
//                     }
//                 }

//                 var hash = rpcClient.GetBlockHash(index);
//                 db.Put(key, Neo.IO.Helper.ToArray(hash).AsSpan(), family);
//                 return hash;
//             }

            
//             public byte[] GetLedgerStorage(ReadOnlyMemory<byte> key)
//             {
//                 var contractHash = Neo.SmartContract.Native.NativeContract.Ledger.Hash;

//                 var familyName = $"{nameof(GetLedgerStorage)}";
//                 var family = GetOrCreateColumnFamily(db, familyName);
//                 using (var slice = db.GetSlice(key.Span, family))
//                 {
//                     if (slice.Valid)
//                     {
//                         return slice.GetValue().ToArray();
//                     }
//                 }

//                 var storage = rpcClient.GetStorage(contractHash, key.Span);
//                 db.Put(key.Span, storage, family);
//                 return storage;
//             }

//             public RpcFoundStates FindStates(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default, int? count = null)
//             {
//                 var familyName = $"{nameof(FindStates)}{rootHash}{scriptHash}";
//                 var family = GetOrCreateColumnFamily(db, familyName);

//                 var keyLength = prefix.Length + from.Length + (count.HasValue ? sizeof(int) : 0);
//                 Span<byte> key = stackalloc byte[keyLength];
//                 prefix.Span.CopyTo(key);
//                 if (from.Length > 0) from.Span.CopyTo(key.Slice(prefix.Length));
//                 if (count.HasValue) BinaryPrimitives.WriteInt32LittleEndian(key.Slice(prefix.Length + from.Length), count.Value);

//                 using (var slice = db.GetSlice(key, family))
//                 {
//                     if (slice.Valid)
//                     {
//                         var token = JToken.Parse(slice.GetValue());
//                         if (token is not null && token is JObject jObject)
//                         {
//                             return RpcFoundStates.FromJson(jObject);
//                         }
//                     }
//                 }

//                 var @params = StateAPI.MakeFindStatesParams(rootHash, scriptHash, prefix.Span, from.Span, count);
//                 var json = (Json.JObject)rpcClient.RpcSend("findstates", @params);
//                 db.Put(key, json.ToByteArray(false), family);
//                 return RpcFoundStates.FromJson(json);
//             }

//             static readonly IMessagePackFormatter<byte[]?> byteArrayFormatter =
//                 MessagePackSerializerOptions.Standard.Resolver.GetFormatter<byte[]?>();

//             ColumnFamilyHandle GetStateColumnFamily(UInt256 rootHash, UInt160 scriptHash)
//             {
//                 var familyName = $"{nameof(GetState)}{rootHash}{scriptHash}";
//                 return GetOrCreateColumnFamily(db, familyName);
//             }

//             // method used for testing
//             internal byte[]? GetCachedState(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> key)
//             {
//                 var family = GetStateColumnFamily(rootHash, scriptHash);
//                 return GetCachedState(key.Span, family);
//             }

//             byte[]? GetCachedState(ReadOnlySpan<byte> key, ColumnFamilyHandle family) => db.Get(key, family);

//             public byte[]? GetState(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> key)
//             {
//                 var family = GetStateColumnFamily(rootHash, scriptHash);
//                 var cachedState = GetCachedState(key.Span, family);

//                 if (cachedState != null)
//                 {
//                     var reader = new MessagePackReader(cachedState);
//                     return byteArrayFormatter.Deserialize(ref reader, MessagePackSerializerOptions.Standard);
//                 }
//                 else
//                 {
//                     var state = rpcClient.GetProvenState(rootHash, scriptHash, key.Span);

//                     var buffer = new ArrayBufferWriter<byte>();
//                     var writer = new MessagePackWriter(buffer);
//                     byteArrayFormatter.Serialize(ref writer, state, MessagePackSerializerOptions.Standard);
//                     writer.Flush();
//                     db.Put(key.Span, buffer.WrittenSpan, family);

//                     return state;
//                 }
//             }
//         }
//     }
// }