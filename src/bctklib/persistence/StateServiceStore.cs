using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Neo.BlockchainToolkit.Models;
using Neo.IO;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;

namespace Neo.BlockchainToolkit.Persistence
{
    public sealed partial class StateServiceStore : IReadOnlyStore, IDisposable
    {
        public interface ICacheClient : IDisposable
        {
            bool TryGetCachedStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[]? value);
            void CacheStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, byte[]? value);
            bool TryGetCachedFoundStates(UInt160 contractHash, byte? prefix, out IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> value);
            void CacheFoundState(UInt160 contractHash, byte? prefix, ReadOnlyMemory<byte> key, byte[] value);
        }

        public const string LoggerCategory = "Neo.BlockchainToolkit.Persistence.StateServiceStore";
        readonly static DiagnosticSource logger;

        static StateServiceStore()
        {
            logger = new DiagnosticListener(LoggerCategory);
        }

        const byte Ledger_Prefix_BlockHash = 9;
        const byte Ledger_Prefix_CurrentBlock = 12;
        const byte Ledger_Prefix_Block = 5;
        const byte Ledger_Prefix_Transaction = 11;
        const byte NEO_Prefix_Candidate = 33;
        const byte NEO_Prefix_GasPerBlock = 29;
        const byte NEO_Prefix_VoterRewardPerCommittee = 23;

        readonly RpcClient rpcClient;
        readonly ICacheClient cacheClient;
        readonly BranchInfo branchInfo;
        readonly IReadOnlyDictionary<int, UInt160> contractMap;
        readonly IReadOnlyDictionary<UInt160, string> contractNameMap;
        bool disposed = false;

        public ProtocolSettings Settings => branchInfo.ProtocolSettings;

        public StateServiceStore(RpcClient rpcClient, ICacheClient cacheClient, BranchInfo branchInfo)
        {
            this.rpcClient = rpcClient;
            this.cacheClient = cacheClient;
            this.branchInfo = branchInfo;
            contractMap = branchInfo.Contracts.ToDictionary(c => c.Id, c => c.Hash);
            contractNameMap = branchInfo.Contracts.ToDictionary(c => c.Hash, c => c.Name);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                rpcClient.Dispose();
                cacheClient.Dispose();
                disposed = true;
            }
        }

        public byte[]? TryGet(byte[]? _key)
        {
            if (disposed) throw new ObjectDisposedException(nameof(StateServiceStore));

            _key ??= Array.Empty<byte>();
            var contractId = BinaryPrimitives.ReadInt32LittleEndian(_key.AsSpan(0, 4));
            var key = _key.AsMemory(4);

            if (contractId == NativeContract.Ledger.Id)
            {
                // Since blocks and transactions are immutable and already available via other
                // JSON-RPC methods, the state service does not store ledger contract data.
                // StateServiceStore needs to translate LedgerContract calls into equivalent
                // non-state service calls.

                // LedgerContract stores the current block's hash and index in a single storage
                // recorded keyed by Ledger_Prefix_CurrentBlock. StateServiceStore is initialized
                // with the branching index, so TryGet needs the associated block hash for this index
                // in order to correctly construct the serialized storage
                var prefix = key.Span[0];
                if (prefix == Ledger_Prefix_CurrentBlock)
                {
                    Debug.Assert(key.Length == 1);

                    var @struct = new VM.Types.Struct() { branchInfo.IndexHash.ToArray(), branchInfo.Index };
                    return BinarySerializer.Serialize(@struct, 1024 * 1024);
                }

                // all other ledger contract prefixes (Block, BlockHash and Transaction) store immutable
                // data, so this data can be directly retrieved from LedgerContract storage
                if (prefix == Ledger_Prefix_Block
                    || prefix == Ledger_Prefix_BlockHash
                    || prefix == Ledger_Prefix_Transaction)
                {
                    if (cacheClient.TryGetCachedStorage(NativeContract.Ledger.Hash, key, out var value)) return value;
                    var result = rpcClient.GetStorage(NativeContract.Ledger.Hash, key.Span);
                    cacheClient.CacheStorage(NativeContract.Ledger.Hash, key, result);
                    return result;
                }

                throw new NotSupportedException(
                    $"{nameof(StateServiceStore)} does not support TryGet method for {nameof(LedgerContract)} with {Convert.ToHexString(key.Span)} key");
            }

            if (contractId == NativeContract.RoleManagement.Id)
            {
                var prefix = key.Span[0];
                if (Enum.IsDefined((Role)prefix))
                {
                    GetFromStates(NativeContract.RoleManagement.Hash, prefix, key);
                }
            }

            if (contractId == NativeContract.NEO.Id)
            {
                var prefix = key.Span[0];
                if (prefix != NEO_Prefix_Candidate
                    && prefix != NEO_Prefix_GasPerBlock
                    && prefix != NEO_Prefix_VoterRewardPerCommittee)
                {
                    GetFromStates(NativeContract.NEO.Hash, prefix, key);
                }
            }

            var contractHash = contractMap[contractId];
            if (contractId < 0)
            {
                if (cacheClient.TryGetCachedStorage(contractHash, key, out var value)) return value;
                var result = rpcClient.GetStorage(contractHash, key.Span);
                cacheClient.CacheStorage(contractHash, key, result);
                return result;
            }

            return GetFromStates(contractHash, null, key);

            byte[]? GetFromStates(UInt160 contractHash, byte? prefix, ReadOnlyMemory<byte> key)
            {
                return FindStates(contractHash, prefix)
                    .FirstOrDefault(kvp => MemorySequenceComparer.Equals(kvp.key.Span, key.Span)).value;
            }
        }

        public bool Contains(byte[]? key) => TryGet(key) is not null;

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? _key, SeekDirection direction)
        {
            if (disposed) throw new ObjectDisposedException(nameof(StateServiceStore));

            _key ??= Array.Empty<byte>();
            var contractId = BinaryPrimitives.ReadInt32LittleEndian(_key.AsSpan(0, 4));
            var key = _key.AsMemory(4);

            if (contractId == NativeContract.Ledger.Id)
            {
                // Because the state service does not store ledger contract data, the seek method cannot
                // be implemented for the ledger contract. As of Neo 3.4, the Ledger contract only 
                // uses Seek in the Initialized method to check for the existence of any value with a
                // Prefix_Block prefix. In order to support this single scenario, return a single empty
                // byte array enumerable. This will enable .Any() LINQ method to return true, but will
                // fail if the caller attempts to deserialize the provided array into a trimmed block.

                var prefix = key.Span[0];
                if (prefix == Ledger_Prefix_Block)
                {
                    Debug.Assert(_key.Length == 5);
                    return Enumerable.Repeat((_key, Array.Empty<byte>()), 1);
                }

                throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method for {nameof(LedgerContract)} with {prefix} prefix");
            }

            if (contractId == NativeContract.RoleManagement.Id)
            {
                var prefix = key.Span[0];
                if (!Enum.IsDefined((Role)prefix))
                {
                    throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method for {nameof(RoleManagement)} with {prefix} prefix");
                }

                var states = FindStates(NativeContract.RoleManagement.Hash, prefix);
                return ConvertStates(states);
            }


            if (contractId == NativeContract.NEO.Id)
            {
                var prefix = key.Span[0];
                if (prefix != NEO_Prefix_Candidate
                    && prefix != NEO_Prefix_GasPerBlock
                    && prefix != NEO_Prefix_VoterRewardPerCommittee)
                {
                    throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method for {nameof(NeoToken)} with {prefix} prefix");
                }

                var states = FindStates(NativeContract.RoleManagement.Hash, prefix);
                return ConvertStates(states);
            }

            if (contractId < 0)
            {
                var contract = branchInfo.Contracts.Single(c => c.Id == contractId);
                throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method for native {contract.Name} contract");
            }

            {
                var states = FindStates(contractMap[contractId]);
                return ConvertStates(states);
            }

            IEnumerable<(byte[] Key, byte[] Value)> ConvertStates(IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> states)
            {
                var comparer = direction == SeekDirection.Forward
                   ? MemorySequenceComparer.Default
                   : MemorySequenceComparer.Reverse;
                throw new Exception();
            }
        }

        public IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> FindStates(UInt160 contractHash, byte? prefix = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(StateServiceStore));

            if (cacheClient.TryGetCachedFoundStates(contractHash, prefix, out var values))
            {
                return values;
            }

            return FindStatesFromService(contractHash, prefix);
        }

        IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> FindStatesFromService(UInt160 contractHash, byte? prefix = null)
        {
            const string loggerName = nameof(FindStatesFromService);
            Activity? activity = null;
            if (logger.IsEnabled(loggerName))
            {
                activity = new Activity(loggerName);
                logger.StartActivity(activity, new { contractHash, prefix });
            }

            var count = 0;
            var stopwatch = Stopwatch.StartNew();

            ReadOnlyMemory<byte> prefixBuffer = default;
            byte[]? rentedBuffer = null;
            if (prefix.HasValue)
            {
                rentedBuffer = ArrayPool<byte>.Shared.Rent(1);
                rentedBuffer[0] = prefix.Value;
                prefixBuffer = rentedBuffer.AsMemory(0, 1);
            }

            try
            {
                var from = Array.Empty<byte>();
                while (true)
                {
                    var found = rpcClient.FindStates(branchInfo.RootHash, contractHash, prefixBuffer.Span, from);
                    if (found.Results.Length > 0)
                    {
                        ValidateProof(branchInfo.RootHash, found.FirstProof, found.Results[0]);
                    }
                    if (found.Results.Length > 1)
                    {
                        ValidateProof(branchInfo.RootHash, found.LastProof, found.Results[^1]);
                    }
                    if (logger.IsEnabled(loggerName) && found.Truncated)
                    {
                        logger.Write($"{loggerName}.Found", found.Results.Length);
                    }
                    count += found.Results.Length;
                    for (int i = 0; i < found.Results.Length; i++)
                    {
                        var (key, value) = found.Results[i];
                        cacheClient.CacheFoundState(contractHash, prefix, key, value);
                        yield return (key, value);
                    }
                    if (!found.Truncated || found.Results.Length == 0) break;
                    from = found.Results[^1].key;
                }
            }
            finally
            {
                stopwatch.Stop();
                if (rentedBuffer is not null) ArrayPool<byte>.Shared.Return(rentedBuffer);
                if (activity is not null) logger.StopActivity(activity, new { count, elapsed = stopwatch.Elapsed });
            }

            static void ValidateProof(UInt256 rootHash, byte[]? proof, (byte[] key, byte[] value) result)
            {
                var (storageKey, storageValue) = Utility.VerifyProof(rootHash, proof);
                if (!result.key.AsSpan().SequenceEqual(storageKey.Key.Span)) throw new Exception("Incorrect StorageKey");
                if (!result.value.AsSpan().SequenceEqual(storageValue)) throw new Exception("Incorrect StorageItem");
            }
        }
    }
}





























// internal interface ICachingClient : IDisposable
// {
//     byte[] GetLedgerStorage(ReadOnlyMemory<byte> key);
//     byte[]? GetNativeContractStorage(UInt160 scriptHash, ReadOnlyMemory<byte> key);
//     // byte[]? GetContractState(UInt160 scriptHash, ReadOnlyMemory<byte> key);
//     // FoundStates FindStates(UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default);
//     // IEnumerable<(byte[] key, byte[] value)> EnumerateStates(UInt160 scriptHash, ReadOnlyMemory<byte> prefix);
// }

// readonly RpcClient rpcClient;
// readonly BranchInfo branchInfo;
// readonly IReadOnlyDictionary<int, UInt160> contractMap;
// bool disposed = false;

// public ProtocolSettings Settings { get; }

// const byte ContractManagement_Prefix_Contract = 8;
// const byte NeoToken_Prefix_GasPerBlock = 29;
// const byte NeoToken_Prefix_VoterRewardPerCommittee = 23;

// public StateServiceStore(string url, in BranchInfo branchInfo, RocksDb? db = null, bool shared = false)
//     : this(GetCachingClient(new Uri(url), branchInfo, db, shared), branchInfo)
// {
// }

// public StateServiceStore(Uri url, in BranchInfo branchInfo, RocksDb? db = null, bool shared = false)
//     : this(GetCachingClient(url, branchInfo, db, shared), branchInfo)
// {
// }

// internal StateServiceStore(RpcClient cachingClient, in BranchInfo branchInfo)
// {
//     this.rpcClient = cachingClient;
//     this.branchInfo = branchInfo;
//     contractMap = branchInfo.Contracts.ToDictionary(c => c.Id, c => c.Hash);
//     Settings = branchInfo.ProtocolSettings;
// }

// static RpcClient GetCachingClient(Uri url, in BranchInfo branchInfo, RocksDb? db, bool shared)
// {
//     return new RpcClient(url);
//     // return new MemoryCacheClient2(rpcClient, branchInfo, shared);
//     // return db is null
//     //     ? new MemoryCacheClient(rpcClient, rootHash, shared) 
//     //     : new RocksDbCacheClient(rpcClient, rootHash, db, shared: shared);
// }

// internal record FoundStates(IReadOnlyList<(byte[] key, byte[] value)> States, bool Truncated);

// static FoundStates FindProvenStates(RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> from)
// {
//     var foundStates = rpcClient.FindStates(rootHash, scriptHash, prefix, from);
//     ValidateFoundStates(rootHash, foundStates);
//     return new FoundStates(foundStates.Results, foundStates.Truncated);
// }

// static void ValidateFoundStates(UInt256 rootHash, RpcFoundStates foundStates)
// {
//     if (foundStates.Results.Length > 0)
//     {
//         ValidateProof(rootHash, foundStates.FirstProof, foundStates.Results[0]);
//     }
//     if (foundStates.Results.Length > 1)
//     {
//         ValidateProof(rootHash, foundStates.LastProof, foundStates.Results[^1]);
//     }

//     static void ValidateProof(UInt256 rootHash, byte[]? proof, (byte[] key, byte[] value) result)
//     {
//         var (storageKey, storageValue) = Utility.VerifyProof(rootHash, proof);
//         if (!result.key.AsSpan().SequenceEqual(storageKey.Key.Span)) throw new Exception("Incorrect StorageKey");
//         if (!result.value.AsSpan().SequenceEqual(storageValue)) throw new Exception("Incorrect StorageItem");
//     }
// }

// internal static byte[]? GetProvenState(RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
// {
//     const int COR_E_KEYNOTFOUND = unchecked((int)0x80131577);

//     try
//     {
//         var result = rpcClient.GetProof(rootHash, scriptHash, key);
//         return Utility.VerifyProof(rootHash, result).value;
//     }
//     // GetProvenState has to match the semantics of IReadOnlyStore.TryGet
//     // which returns null for invalid keys instead of throwing an exception.
//     catch (RpcException ex) when (ex.HResult == COR_E_KEYNOTFOUND)
//     {
//         // Trie class throws KeyNotFoundException if key is not in the trie.
//         // RpcClient/Server converts the KeyNotFoundException into an
//         // RpcException with code == COR_E_KEYNOTFOUND.

//         return null;
//     }
//     catch (RpcException ex) when (ex.HResult == -100 && ex.Message == "Unknown value")
//     {
//         // Prior to Neo 3.3.0, StateService GetProof method threw a custom exception 
//         // instead of KeyNotFoundException like GetState. This catch clause detected
//         // the custom exception that GetProof used to throw. 

//         // TODO: remove this clause once deployed StateService for Neo N3 MainNet and
//         //       TestNet has been verified to be running Neo 3.3.0 or later.

//         return null;
//     }
// }

// public static async Task<BranchInfo> GetBranchInfoAsync(RpcClient rpcClient, uint index)
// {
//     var versionTask = rpcClient.GetVersionAsync();
//     var blockHashTask = rpcClient.GetBlockHashAsync(index);
//     var stateRoot = await rpcClient.GetStateRootAsync(index).ConfigureAwait(false);
//     var contractsTask = GetContracts(rpcClient, stateRoot.RootHash);

//     await Task.WhenAll(versionTask, blockHashTask, contractsTask).ConfigureAwait(false);

//     var version = await versionTask.ConfigureAwait(false);
//     var blockHash = await blockHashTask.ConfigureAwait(false);
//     var contracts = await contractsTask.ConfigureAwait(false);

//     return new BranchInfo(
//         version.Protocol.Network,
//         version.Protocol.AddressVersion,
//         index,
//         blockHash,
//         stateRoot.RootHash,
//         contracts);

//     static async Task<IReadOnlyList<ContractInfo>> GetContracts(RpcClient rpcClient, UInt256 rootHash)
//     {
//         const byte ContractManagement_Prefix_Contract = 8;

//         using var memoryOwner = MemoryPool<byte>.Shared.Rent(1);
//         memoryOwner.Memory.Span[0] = ContractManagement_Prefix_Contract;
//         var prefix = memoryOwner.Memory[..1];

//         var contracts = new List<ContractInfo>();
//         var from = Array.Empty<byte>();
//         while (true)
//         {
//             var found = await rpcClient.FindStatesAsync(rootHash, NativeContract.ContractManagement.Hash, prefix, from.AsMemory()).ConfigureAwait(false);
//             ValidateFoundStates(rootHash, found);
//             for (int i = 0; i < found.Results.Length ; i++)
//             {
//                 var (key, value) = found.Results[i];
//                 if (key.AsSpan().StartsWith(prefix.Span))
//                 {
//                     var state = new StorageItem(value).GetInteroperable<ContractState>();
//                     contracts.Add(new ContractInfo(state.Id, state.Hash, state.Manifest.Name));
//                 }
//             }
//             if (!found.Truncated || found.Results.Length == 0) break;
//             from = found.Results[^1].key;
//         }
//         return contracts;
//     }
// }

// public void Dispose()
// {
//     rpcClient.Dispose();
//     GC.SuppressFinalize(this);
// }


// static bool ShouldRetrieveFullContractStorage(int contractId, ReadOnlyMemory<byte> key)
// {
//     // always retrieve full contract storage for deployed contracts
//     if (contractId >= 0) return true;

//     // for RoleManagement contract, awl
//     if (contractId == NativeContract.RoleManagement.Id)
//     {
//         Debug.Assert(Enum.IsDefined((Role)key.Span[0]));
//         return true;
//     }
//     if (contractId == NativeContract.NEO.Id)
//     {
//         const byte Prefix_Candidate = 33;
//         const byte Prefix_GasPerBlock = 29;
//         const byte Prefix_VoterRewardPerCommittee = 23;

//         var prefix = key.Span[0];
//         return prefix == Prefix_Candidate
//             || prefix == Prefix_GasPerBlock
//             || prefix == Prefix_VoterRewardPerCommittee;
//     }
//     return false;
// }

// public byte[]? TryGet(byte[] key)
// {
//     if (disposed) throw new ObjectDisposedException(nameof(StateServiceStore));

//     var contractId = BinaryPrimitives.ReadInt32LittleEndian(key.AsSpan(0, 4));

//     if (contractId == NativeContract.Ledger.Id)
//     {
//         // Since blocks and transactions are immutable and already available via other
//         // JSON-RPC methods, the state service does not store ledger contract data.
//         // StateServiceStore needs to translate LedgerContract calls into equivalent
//         // non-state service calls.

//         // LedgerContract stores the current block's hash and index in a single storage
//         // recorded keyed by Ledger_Prefix_CurrentBlock. StateServiceStore is initialized
//         // with the branching index, so TryGet needs the associated block hash for this index
//         // in order to correctly construct the serialized storage

//         if (key[4] == Ledger_Prefix_CurrentBlock)
//         {
//             Debug.Assert(key.Length == 5);

//             var @struct = new VM.Types.Struct() { branchInfo.IndexHash.ToArray(), branchInfo.Index };
//             return BinarySerializer.Serialize(@struct, 1024 * 1024);
//         }

//         // all other ledger contract prefixes (Block, BlockHash and Transaction) store immutable
//         // data, so this data can be directly retrieved from LedgerContract storage
//         Debug.Assert(key[4] == Ledger_Prefix_Block
//             || key[4] == Ledger_Prefix_BlockHash
//             || key[4] == Ledger_Prefix_Transaction);

//         return rpcClient.GetStorage(NativeContract.Ledger.Hash, key.AsSpan(4));
//     }

//     if (contractId < 0)
//     {
//          // for native contracts (other than ledger)
//          if (contractId == NativeContract.ContractManagement.Id
//             )
//     }







//     if (contractId == NativeContract.RoleManagement.Id)
//     {

//     }

//     // for all other contracts, we simply need to translate the contract ID into the contract
//     // hash and call the State Service GetState method.

//     if (contractMap.TryGetValue(contractId, out var contract))
//     {
//         return rpcClient.GetContractState(contract, key.AsMemory(4));
//     }

//     throw new InvalidOperationException($"Invalid contract ID {contractId}");
// }

// public bool Contains(byte[] key) => TryGet(key) != null;

// class LedgerBlockEnumerable : IEnumerable<(byte[] Key, byte[] Value)>
// {
//     public IEnumerator<(byte[] Key, byte[] Value)> GetEnumerator()
//     {
//         return new Enumerator();
//     }

//     IEnumerator IEnumerable.GetEnumerator()
//     {
//         return new Enumerator();
//     }

//     class Enumerator : IEnumerator<(byte[] Key, byte[] Value)>
//     {
//         bool moveNext = true;

//         public (byte[] Key, byte[] Value) Current => throw new NotSupportedException();

//         object IEnumerator.Current => throw new NotSupportedException();

//         public void Dispose()
//         {
//         }

//         public bool MoveNext()
//         {
//             var prev = moveNext;
//             moveNext = false;
//             return prev;
//         }

//         public void Reset()
//         {
//             moveNext = true;
//         }
//     }
// }

// public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] key, SeekDirection direction)
// {
//     if (disposed) throw new ObjectDisposedException(nameof(StateServiceStore));

//     if (key.Length == 0 && direction == SeekDirection.Backward)
//     {
//         return Enumerable.Empty<(byte[] key, byte[] value)>();
//     }

//     var contractId = BinaryPrimitives.ReadInt32LittleEndian(key.AsSpan(0, 4));
//     if (contractId < 0)
//     {
//         // Because the state service does not store ledger contract data, the seek method cannot
//         // be implemented efficiently for the ledger contract. Luckily, the Ledger contract only
//         // uses Seek in Initialized to check for the existence of any value with the Prefix_Block
//         // prefix. For this one scenario, return a single empty value so the .Any() method returns true

//         if (contractId == NativeContract.Ledger.Id)
//         {

//             if (key[4] == Ledger_Prefix_Block)
//             {
//                 Debug.Assert(key.Length == 5);
//                 return new LedgerBlockEnumerable();
//             }

//             throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method for native Ledger contract");
//         }

//         // There are several places in the neo code base that search backwards between a range
//         // byte array keys. In these cases, the Seek key parameter represents the *end* of the 
//         // range being searched. The FindStates State Service method doesn't support seeking 
//         // backwards like this, so for these cases we need to determine the case specific prefix,
//         // use State Service to retrieve all the values with this prefix and then reverse their 
//         // order in memory before passing back to the Seek call site.

//         if (direction == SeekDirection.Backward)
//         {
//             if (contractId == NativeContract.NEO.Id)
//             {
//                 // NEO contract VoterRewardPerCommittee keys are searched by voter ECPoint (33 bytes long).
//                 // For this case, use 1 byte VoterRewardPerCommittee prefix plus 33 bytes voter ECPoint for seek operation prefix

//                 if (key[4] == NeoToken_Prefix_VoterRewardPerCommittee)
//                 {

//                     if (key.Length < 38) throw new InvalidOperationException("Invalid NeoToken VoterRewardPerCommittee key");
//                     return Seek(NativeContract.NEO.Hash, contractId, key.AsMemory(4, 34), direction);
//                 }

//                 // NEO contract GasPerBlock keys are searched using the GasPerBlock prefix.
//                 // For this case, use 1 byte GasPerBlock prefix as the seek operation prefix.

//                 if (key[4] == NeoToken_Prefix_GasPerBlock)
//                 {
//                     return Seek(NativeContract.NEO.Hash, contractId, key.AsMemory(4, 1), direction);
//                 }
//             }

//             // RoleManagement contract Role keys are searched using the Role prefix.
//             // For this case, use 1 byte Role prefix as the seek operation prefix.

//             if (contractId == NativeContract.RoleManagement.Id
//                 && Enum.IsDefined<Role>((Role)key[4]))
//             {
//                 return Seek(NativeContract.RoleManagement.Hash, contractId, key.AsMemory(4, 1), direction);
//             }

//             // If the backwards seek call does not match one of the three cases specified above,
//             // it is not supported by the StateServiceStore

//             throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method with backwards direction parameter.");
//         }
//     }

//     if (contractMap.TryGetValue(contractId, out var contract))
//     {
//         return Seek(contract, contractId, key.AsMemory(4), direction);
//     }

//     throw new InvalidOperationException($"Invalid contract ID {contractId}");
// }

// // IEnumerable<(byte[] key, byte[] value)> EnumerateStates(UInt160 scriptHash, ReadOnlyMemory<byte> prefix)
// // {
// //     var from = Array.Empty<byte>();
// //     while (true)
// //     {
// //         var foundStates = cachingClient.FindStates(scriptHash, prefix, from);
// //         for (int i = 0; i < foundStates.States.Count; i++)
// //         {
// //             yield return foundStates.States[i];
// //         }
// //         if (!foundStates.Truncated || foundStates.States.Count == 0) break;
// //         from = foundStates.States[^1].key;
// //     }
// // }

// IEnumerable<(byte[] Key, byte[] Value)> Seek(UInt160 contractHash, int contractId, ReadOnlyMemory<byte> prefix, SeekDirection direction)
// {
//     var comparer = direction == SeekDirection.Forward
//         ? MemorySequenceComparer.Default
//         : MemorySequenceComparer.Reverse;

//     return rpcClient.EnumerateStates(contractHash, prefix)
//         .Select(kvp =>
//         {
//             var k = new byte[kvp.key.Length + 4];
//             BinaryPrimitives.WriteInt32LittleEndian(k.AsSpan(0, 4), contractId);
//             kvp.key.CopyTo(k.AsSpan(4));
//             return (key: k, kvp.value);
//         })
//         .OrderBy(kvp => kvp.key, comparer);
// }
