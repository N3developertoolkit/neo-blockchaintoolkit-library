// using System;
// using System.Buffers;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.IO;
// using DotNext.Buffers;
// using DotNext.IO;
// using Neo.IO;
// using Neo.Network.RPC;
// using Neo.SmartContract.Native;
// using RocksDbSharp;

// namespace Neo.BlockchainToolkit.Persistence
// {
//     public partial class StateServiceStore
//     {
//         internal class RocksDbCacheClient : ICachingClient
//         {
//             readonly RpcClient rpcClient;
//             readonly UInt256 rootHash;
//             readonly RocksDb db;
//             readonly ColumnFamilyHandle findStatesColumnFamily;
//             readonly ColumnFamilyHandle getStateColumnFamily;
//             readonly ColumnFamilyHandle ledgerStorageColumnFamily;
//             readonly bool shared;
//             bool disposed = false;

//             public RocksDbCacheClient(RpcClient rpcClient, UInt256 rootHash, RocksDb db, string? columnFamilyPrefix = null, bool shared = false)
//             {
//                 this.rpcClient = rpcClient;
//                 this.rootHash = rootHash;
//                 this.db = db;
//                 this.shared = shared;

//                 columnFamilyPrefix = string.IsNullOrEmpty(columnFamilyPrefix)
//                     ? nameof(RocksDbCacheClient)
//                     : columnFamilyPrefix;

//                 findStatesColumnFamily = db.GetOrCreateColumnFamily(columnFamilyPrefix + "." + nameof(FindStates));
//                 getStateColumnFamily = db.GetOrCreateColumnFamily(columnFamilyPrefix + "." + nameof(GetContractState));
//                 ledgerStorageColumnFamily = db.GetOrCreateColumnFamily(columnFamilyPrefix + "." + nameof(GetLedgerStorage));
//             }

//             public void Dispose()
//             {
//                 if (disposed) return;
//                 disposed = true;
//                 if (!shared)
//                 {
//                     rpcClient.Dispose();
//                     db.Dispose();
//                     GC.SuppressFinalize(this);
//                 }
//             }

//             public IEnumerable<(byte[] key, byte[] value)> EnumerateStates(UInt160 scriptHash, ReadOnlyMemory<byte> prefix)
//             {
//                 throw new Exception();
//             }


//             public FoundStates FindStates(UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default)
//             {
//                 if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
//                 var dbKey = new ArrayBufferWriter<byte>(UInt160.Length + prefix.Length + from.Length);
//                 {
//                     using var writer = new BinaryWriter(dbKey.AsStream());
//                     scriptHash.Serialize(writer);
//                     writer.Write(prefix.Span);
//                     writer.Write(from.Span);
//                     writer.Flush();
//                 }

//                 using (var slice = db.GetSlice(dbKey.WrittenSpan, findStatesColumnFamily))
//                 {
//                     if (slice.Valid)
//                     {
//                         var reader = new SpanReader<byte>(slice.GetValue());
//                         var count = reader.ReadVarInt();
//                         var states = new List<(byte[], byte[])>();
//                         for (ulong i = 0; i < count; i++)
//                         {
//                             var key = reader.ReadVarBytes();
//                             var value = reader.ReadVarBytes();
//                             states.Add((key, value));
//                         }
//                         var truncated = reader.ReadBoolean();
//                         Debug.Assert(reader.RemainingSpan.Length == 0);
//                         return new FoundStates(states, truncated);
//                     }
//                 }

//                 var found = FindProvenStates(rpcClient, rootHash, scriptHash, prefix.Span, from.Span);
//                 var valueSeq = new Nerdbank.Streams.Sequence<byte>();
//                 {
//                     using var writer = new BinaryWriter(valueSeq.AsStream());
//                     writer.WriteVarInt(found.States.Count);
//                     for (int i = 0; i < found.States.Count; i++)
//                     {
//                         var (key, value) = found.States[i];
//                         writer.WriteVarBytes(key);
//                         writer.WriteVarBytes(value);
//                     }
//                     writer.Write(found.Truncated);
//                     writer.Flush();
//                 }

//                 using var batch = new WriteBatch();
//                 batch.PutVector(dbKey.WrittenMemory, valueSeq.AsReadOnlySequence, findStatesColumnFamily);
//                 db.Write(batch);

//                 return found;
//             }

//             const byte NON_NULL_PREFIX = 1;
//             const byte NULL_PREFIX = 0;
//             readonly static ReadOnlyMemory<byte> nullPrefix = (new byte[] { NULL_PREFIX }).AsMemory();
//             readonly static ReadOnlyMemory<byte> notNullPrefix = (new byte[] { NON_NULL_PREFIX }).AsMemory();

//             public byte[]? GetContractState(UInt160 scriptHash, ReadOnlyMemory<byte> key)
//             {
//                 if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
//                 var dbKey = new ArrayBufferWriter<byte>(UInt160.Length + key.Length);
//                 {
//                     using var writer = new BinaryWriter(dbKey.AsStream());
//                     scriptHash.Serialize(writer);
//                     writer.Write(key.Span);
//                     writer.Flush();
//                 }

//                 using (var slice = db.GetSlice(dbKey.WrittenSpan, getStateColumnFamily))
//                 {
//                     if (slice.Valid)
//                     {
//                         var span = slice.GetValue();
//                         if (span.Length == 1 && span[0] == NULL_PREFIX) return null;
//                         return span[1..].ToArray();
//                     }
//                 }

//                 var state = GetProvenState(rpcClient, rootHash, scriptHash, key.Span);
//                 if (state is null)
//                 {
//                     db.Put(dbKey.WrittenSpan, nullPrefix.Span, getStateColumnFamily);
//                 }
//                 else
//                 {
//                     using var batch = new WriteBatch();
//                     batch.PutVector(getStateColumnFamily, dbKey.WrittenMemory, notNullPrefix, state.AsMemory());
//                     db.Write(batch);
//                 }
//                 return state;
//             }

//             public byte[] GetLedgerStorage(ReadOnlyMemory<byte> key)
//             {
//                 if (disposed || db.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(RocksDbStore));
//                 using (var slice = db.GetSlice(key.Span, ledgerStorageColumnFamily))
//                 {
//                     if (slice.Valid)
//                     {
//                         return slice.GetValue().ToArray();
//                     }
//                 }

//                 var storage = rpcClient.GetStorage(NativeContract.Ledger.Hash, key.Span);
//                 db.Put(key.Span, storage, ledgerStorageColumnFamily);
//                 return storage;
//             }
//         }
//     }
// }
