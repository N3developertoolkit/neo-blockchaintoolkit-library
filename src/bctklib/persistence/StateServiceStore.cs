using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Models;
using Neo.IO;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore : IReadOnlyStore, IDisposable
    {
        internal interface ICachingClient : IDisposable
        {
            byte[] GetLedgerStorage(ReadOnlyMemory<byte> key);
            byte[]? GetContractState(UInt160 scriptHash, ReadOnlyMemory<byte> key);
            FoundStates FindStates(UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default);
        }

        readonly ICachingClient cachingClient;
        readonly uint index;
        readonly UInt256 indexHash;
        readonly IReadOnlyDictionary<int, UInt160> contractMap;

        public ProtocolSettings Settings { get; }

        const byte ContractManagement_Prefix_Contract = 8;
        const byte Ledger_Prefix_BlockHash = 9;
        const byte Ledger_Prefix_CurrentBlock = 12;
        const byte Ledger_Prefix_Block = 5;
        const byte Ledger_Prefix_Transaction = 11;
        const byte NeoToken_Prefix_GasPerBlock = 29;
        const byte NeoToken_Prefix_VoterRewardPerCommittee = 23;

        public StateServiceStore(string url, in BranchInfo branchInfo, RocksDb? db = null, bool shared = false)
            : this(GetCachingClient(new Uri(url), branchInfo.RootHash, db, shared), branchInfo)
        {
        }

        public StateServiceStore(Uri url, in BranchInfo branchInfo, RocksDb? db = null, bool shared = false)
            : this(GetCachingClient(url, branchInfo.RootHash, db, shared), branchInfo)
        {
        }

        internal StateServiceStore(ICachingClient cachingClient, in BranchInfo branchInfo)
        {
            this.cachingClient = cachingClient;
            index = branchInfo.Index;
            indexHash = branchInfo.IndexHash;
            contractMap = branchInfo.ContractMap;
            Settings = branchInfo.ProtocolSettings;
        }

        static ICachingClient GetCachingClient(Uri url, UInt256 rootHash, RocksDb? db, bool shared)
        {
            var rpcClient = new RpcClient(url);
            return db is null
                ? new MemoryCacheClient(rpcClient, rootHash, shared) 
                : new RocksDbCacheClient(rpcClient, rootHash, db, shared: shared);
        }

        internal record FoundStates(IReadOnlyList<(byte[] key, byte[] value)> States, bool Truncated);

        static FoundStates FindProvenStates(RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> from)
        {
            var foundStates = rpcClient.FindStates(rootHash, scriptHash, prefix, from);
            ValidateFoundStates(rootHash, foundStates);
            return new FoundStates(foundStates.Results, foundStates.Truncated);
        }

        static void ValidateFoundStates(UInt256 rootHash, RpcFoundStates foundStates)
        {
            if (foundStates.Results.Length > 0)
            {
                ValidateProof(rootHash, foundStates.FirstProof, foundStates.Results[0]);
            }
            if (foundStates.Results.Length > 1)
            {
                ValidateProof(rootHash, foundStates.LastProof, foundStates.Results[^1]);
            }

            static void ValidateProof(UInt256 rootHash, byte[]? proof, (byte[] key, byte[] value) result)
            {
                var (storageKey, storageValue) = Utility.VerifyProof(rootHash, proof);
                if (!result.key.AsSpan().SequenceEqual(storageKey.Key.Span)) throw new Exception("Incorrect StorageKey");
                if (!result.value.AsSpan().SequenceEqual(storageValue)) throw new Exception("Incorrect StorageItem");
            }
        }

        internal static byte[]? GetProvenState(RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
        {
            const int COR_E_KEYNOTFOUND = unchecked((int)0x80131577);

            try
            {
                var result = rpcClient.GetProof(rootHash, scriptHash, key);
                return Utility.VerifyProof(rootHash, result).value;
            }
            // GetProvenState has to match the semantics of IReadOnlyStore.TryGet
            // which returns null for invalid keys instead of throwing an exception.
            catch (RpcException ex) when (ex.HResult == COR_E_KEYNOTFOUND)
            {
                // Trie class throws KeyNotFoundException if key is not in the trie.
                // RpcClient/Server converts the KeyNotFoundException into an
                // RpcException with code == COR_E_KEYNOTFOUND.

                return null;
            }
            catch (RpcException ex) when (ex.HResult == -100 && ex.Message == "Unknown value")
            {
                // Prior to Neo 3.3.0, StateService GetProof method threw a custom exception 
                // instead of KeyNotFoundException like GetState. This catch clause detected
                // the custom exception that GetProof used to throw. 

                // TODO: remove this clause once deployed StateService for Neo N3 MainNet and
                //       TestNet has been verified to be running Neo 3.3.0 or later.

                return null;
            }
        }

        public static async Task<BranchInfo> GetBranchInfoAsync(RpcClient rpcClient, uint index)
        {
            var versionTask = rpcClient.GetVersionAsync();
            var blockHashTask = rpcClient.GetBlockHashAsync(index);
            var stateRoot = await rpcClient.GetStateRootAsync(index).ConfigureAwait(false);
            var contractMapTask = GetContractMap(rpcClient, stateRoot.RootHash);

            await Task.WhenAll(versionTask, blockHashTask, contractMapTask).ConfigureAwait(false);

            var version = await versionTask.ConfigureAwait(false);
            var blockHash = await blockHashTask.ConfigureAwait(false);
            var contractMap = await contractMapTask.ConfigureAwait(false);

            return new BranchInfo(
                version.Protocol.Network,
                version.Protocol.AddressVersion,
                index,
                blockHash,
                stateRoot.RootHash,
                contractMap);

            static async Task<IReadOnlyDictionary<int, UInt160>> GetContractMap(RpcClient rpcClient, UInt256 rootHash)
            {
                const byte ContractManagement_Prefix_Contract = 8;

                using var memoryOwner = MemoryPool<byte>.Shared.Rent(1);
                memoryOwner.Memory.Span[0] = ContractManagement_Prefix_Contract;
                var prefix = memoryOwner.Memory[..1];

                var contractMapBuilder = System.Collections.Immutable.ImmutableDictionary.CreateBuilder<int, UInt160>();
                var from = Array.Empty<byte>();
                while (true)
                {
                    var found = await rpcClient.FindStatesAsync(rootHash, NativeContract.ContractManagement.Hash, prefix, from.AsMemory()).ConfigureAwait(false);
                    ValidateFoundStates(rootHash, found);
                    for (int i = 0; i < found.Results.Length ; i++)
                    {
                        var (key, value) = found.Results[i];
                        if (key.AsSpan().StartsWith(prefix.Span))
                        {
                            var state = new StorageItem(value).GetInteroperable<ContractState>();
                            contractMapBuilder.Add(state.Id, state.Hash);
                        }
                    }
                    if (!found.Truncated || found.Results.Length == 0) break;
                    from = found.Results[^1].key;
                }
                return contractMapBuilder.ToImmutable();
            }
        }

        public void Dispose()
        {
            cachingClient.Dispose();
            GC.SuppressFinalize(this);
        }

        public byte[]? TryGet(byte[] key)
        {
            var contractId = BinaryPrimitives.ReadInt32LittleEndian(key.AsSpan(0, 4));

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

                if (key[4] == Ledger_Prefix_CurrentBlock)
                {
                    Debug.Assert(key.Length == 5);

                    var @struct = new VM.Types.Struct() { indexHash.ToArray(), index };
                    return BinarySerializer.Serialize(@struct, 1024 * 1024);
                }

                // all other ledger contract prefixes (Block, BlockHash and Transaction) store immutable
                // data, so this data can be directly retrieved from LedgerContract storage
                Debug.Assert(key[4] == Ledger_Prefix_Block
                    || key[4] == Ledger_Prefix_BlockHash
                    || key[4] == Ledger_Prefix_Transaction);

                return cachingClient.GetLedgerStorage(key.AsMemory(4));
            }

            // for all other contracts, we simply need to translate the contract ID into the contract
            // hash and call the State Service GetState method.

            if (contractMap.TryGetValue(contractId, out var contract))
            {
                return cachingClient.GetContractState(contract, key.AsMemory(4));
            }

            throw new InvalidOperationException($"Invalid contract ID {contractId}");
        }

        public bool Contains(byte[] key) => TryGet(key) != null;

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] key, SeekDirection direction)
        {
            if (key.Length == 0 && direction == SeekDirection.Backward)
            {
                return Enumerable.Empty<(byte[] key, byte[] value)>();
            }

            var contractId = BinaryPrimitives.ReadInt32LittleEndian(key.AsSpan(0, 4));
            if (contractId < 0)
            {
                // Because the state service does not store ledger contract data, the seek method cannot
                // be implemented efficiently for the ledger contract. Luckily, the Ledger contract only
                // uses Seek in Initialized to check for the existence of any value with the Prefix_Block
                // prefix. For this one scenario, return a single empty value so the .Any() method returns true

                if (contractId == NativeContract.Ledger.Id)
                {

                    if (key[4] == Ledger_Prefix_Block)
                    {
                        Debug.Assert(key.Length == 5);
                        return Enumerable.Repeat((key, Array.Empty<byte>()), 1);
                    }

                    throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method for native Ledger contract");
                }

                // There are several places in the neo code base that search backwards between a range
                // byte array keys. In these cases, the Seek key parameter represents the *end* of the 
                // range being searched. The FindStates State Service method doesn't support seeking 
                // backwards like this, so for these cases we need to determine the case specific prefix,
                // use State Service to retrieve all the values with this prefix and then reverse their 
                // order in memory before passing back to the Seek call site.

                if (direction == SeekDirection.Backward)
                {
                    if (contractId == NativeContract.NEO.Id)
                    {
                        // NEO contract VoterRewardPerCommittee keys are searched by voter ECPoint (33 bytes long).
                        // For this case, use 1 byte VoterRewardPerCommittee prefix plus 33 bytes voter ECPoint for seek operation prefix

                        if (key[4] == NeoToken_Prefix_VoterRewardPerCommittee)
                        {

                            if (key.Length < 38) throw new InvalidOperationException("Invalid NeoToken VoterRewardPerCommittee key");
                            return Seek(NativeContract.NEO.Hash, contractId, key.AsMemory(4, 34), direction);
                        }

                        // NEO contract GasPerBlock keys are searched using the GasPerBlock prefix.
                        // For this case, use 1 byte GasPerBlock prefix as the seek operation prefix.

                        if (key[4] == NeoToken_Prefix_GasPerBlock)
                        {
                            return Seek(NativeContract.NEO.Hash, contractId, key.AsMemory(4, 1), direction);
                        }
                    }

                    // RoleManagement contract Role keys are searched using the Role prefix.
                    // For this case, use 1 byte Role prefix as the seek operation prefix.

                    if (contractId == NativeContract.RoleManagement.Id
                        && Enum.IsDefined<Role>((Role)key[4]))
                    {
                        return Seek(NativeContract.RoleManagement.Hash, contractId, key.AsMemory(4, 1), direction);
                    }

                    // If the backwards seek call does not match one of the three cases specified above,
                    // it is not supported by the StateServiceStore

                    throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method with backwards direction parameter.");
                }
            }

            if (contractMap.TryGetValue(contractId, out var contract))
            {
                return Seek(contract, contractId, key.AsMemory(4), direction);
            }

            throw new InvalidOperationException($"Invalid contract ID {contractId}");
        }

        IEnumerable<(byte[] key, byte[] value)> EnumerateStates(UInt160 scriptHash, ReadOnlyMemory<byte> prefix)
        {
            var from = Array.Empty<byte>();
            while (true)
            {
                var foundStates = cachingClient.FindStates(scriptHash, prefix, from);
                for (int i = 0; i < foundStates.States.Count; i++)
                {
                    yield return foundStates.States[i];
                }
                if (!foundStates.Truncated || foundStates.States.Count == 0) break;
                from = foundStates.States[^1].key;
            }
        }

        IEnumerable<(byte[] Key, byte[] Value)> Seek(UInt160 contractHash, int contractId, ReadOnlyMemory<byte> prefix, SeekDirection direction)
        {
            var comparer = direction == SeekDirection.Forward
                ? MemorySequenceComparer.Default
                : MemorySequenceComparer.Reverse;

            return EnumerateStates(contractHash, prefix)
                .Select(kvp =>
                {
                    var k = new byte[kvp.key.Length + 4];
                    BinaryPrimitives.WriteInt32LittleEndian(k.AsSpan(0, 4), contractId);
                    kvp.key.CopyTo(k.AsSpan(4));
                    return (key: k, kvp.value);
                })
                .OrderBy(kvp => kvp.key, comparer);
        }
    }
}
