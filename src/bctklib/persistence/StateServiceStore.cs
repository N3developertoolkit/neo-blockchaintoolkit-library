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
    public partial class StateServiceStore : IReadOnlyStore, IDisposable
    {
        internal interface ICachingClient : IDisposable
        {
            byte[] GetStorage(UInt160 contractHash, ReadOnlyMemory<byte> key);
            byte[]? GetProvenState(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> key);
            RpcFoundStates FindStates(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default, int? count = null);
        }

        readonly RpcClient rpcClient;
        readonly uint index;
        readonly UInt256 indexHash;
        readonly UInt256 rootHash;
        readonly IReadOnlyDictionary<int, UInt160> contractMap;

        public ProtocolSettings Settings { get; }

        const byte ContractManagement_Prefix_Contract = 8;
        const byte Ledger_Prefix_BlockHash = 9;
        const byte Ledger_Prefix_CurrentBlock = 12;
        const byte Ledger_Prefix_Block = 5;
        const byte Ledger_Prefix_Transaction = 11;
        const byte NeoToken_Prefix_GasPerBlock = 29;
        const byte NeoToken_Prefix_VoterRewardPerCommittee = 23;

        public StateServiceStore(string url, in BranchInfo branchInfo, string? cachePath = null)
            : this(GetCachingClient(new Uri(url), cachePath), branchInfo)
        {
        }

        public StateServiceStore(Uri url, in BranchInfo branchInfo, string? cachePath = null)
            : this(GetCachingClient(url, cachePath), branchInfo)
        {
        }

        internal StateServiceStore(RpcClient rpcClient, in BranchInfo branchInfo)
        {
            this.rpcClient = rpcClient;
            index = branchInfo.Index;
            indexHash = branchInfo.IndexHash;
            rootHash = branchInfo.RootHash;
            contractMap = branchInfo.ContractMap;
            Settings = branchInfo.ProtocolSettings;
        }

        static RpcClient GetCachingClient(Uri url, string? cachePath)
        {
            // TODO: fill in this code
            return new RpcClient(url);
        }

        public void Dispose()
        {
            rpcClient.Dispose();
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

                return rpcClient.GetStorage(NativeContract.Ledger.Hash, key.AsSpan(4));
            }

            // for all other contracts, we simply need to translate the contract ID into the contract
            // hash and call the State Service GetState method.

            if (contractMap.TryGetValue(contractId, out var contract))
            {
                return rpcClient.GetProvenState(rootHash, contract, key.AsSpan(4));
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

        IEnumerable<(byte[] key, byte[] value)> EnumerateStates(UInt160 scriptHash, ReadOnlyMemory<byte> prefix, int? pageSize = null)
        {
            var from = Array.Empty<byte>();
            while (true)
            {
                var foundStates = rpcClient.FindStates(rootHash, scriptHash, prefix.Span, from, pageSize);
                var states = RpcClientExtensions.ValidateFoundStates(rootHash, foundStates);
                for (int i = 0; i < states.Length; i++)
                {
                    yield return states[i];
                }
                if (!foundStates.Truncated || states.Length == 0) break;
                from = states[^1].key;
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
