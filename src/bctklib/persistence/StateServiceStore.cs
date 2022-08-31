using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Neo.IO;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;

namespace Neo.BlockchainToolkit.Persistence
{
    using ContractMap = IReadOnlyDictionary<int, (UInt160 hash, ContractManifest manifest)>;

    public partial class StateServiceStore : IReadOnlyStore, IDisposable
    {
        interface ICachingClient : IDisposable
        {
            RpcFoundStates FindStates(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default, int? count = null);
            UInt256 GetBlockHash(uint index);
            byte[]? GetState(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> key);
            Task<UInt256> GetStateRootHashAsync(uint index);
            byte[] GetLedgerStorage(ReadOnlyMemory<byte> key);
        }

        readonly ICachingClient cachingClient;
        readonly uint index;
        readonly UInt256 rootHash;
        readonly ContractMap contractMap;

        public ProtocolSettings Settings { get; }

        const byte ContractManagement_Prefix_Contract = 8;
        const byte Ledger_Prefix_BlockHash = 9;
        const byte Ledger_Prefix_CurrentBlock = 12;
        const byte Ledger_Prefix_Block = 5;
        const byte Ledger_Prefix_Transaction = 11;
        const byte NeoToken_Prefix_GasPerBlock = 29;
        const byte NeoToken_Prefix_VoterRewardPerCommittee = 23;

        StateServiceStore(ICachingClient cachingClient, uint index, UInt256 rootHash, ProtocolSettings settings, ContractMap contractMap)
        {
            this.cachingClient = cachingClient;
            this.index = index;
            this.rootHash = rootHash;
            this.Settings = settings;
            this.contractMap = contractMap;
        }

        public static Task<StateServiceStore> CreateAsync(string uri, uint index, string? cachePath = null)
        {
            return CreateAsync(new Uri(uri), index, cachePath);
        }

        public static async Task<StateServiceStore> CreateAsync(Uri uri, uint index, string? cachePath = null)
        {
            // create temporary RpcClient in order to get network version info 
            // and configure protocol settings correctly
            var settings = await GetSettingsAsync(uri).ConfigureAwait(false);

            var rpcClient = new RpcClient(uri, protocolSettings: settings);
            ICachingClient cachingClient = string.IsNullOrEmpty(cachePath)
                ? new MemoryCacheClient(rpcClient)
                : new RocksDbCacheClient(rpcClient, cachePath);
            var stateRootHash = await cachingClient.GetStateRootHashAsync(index).ConfigureAwait(false);

            // IReadOnlyStore key parameters identifies contracts by their internal 32 bit signed integer
            // ID while the State Service identifies contracts by their UInt160 contract hash. So we 
            // build a dictionary mapping internal contract IDs to the manifest (including the contract
            // hash) so we can construct the correct state service calls.

            using var memoryOwner = MemoryPool<byte>.Shared.Rent(1);
            memoryOwner.Memory.Span[0] = ContractManagement_Prefix_Contract;
            var prefix = memoryOwner.Memory.Slice(0, 1);

            // ICachingClient.FindStates is typically used in a synchronous context. It doesn't seem worth it
            // to have a separate async version just for retrieving the contract map. Instead, we'll run the sync
            // version on a BG task in order to keep the create method async. It's slightly wasteful to use an
            // extra task like this instead of using the async methods of RpcClient/HttpClient, but it's a one
            // time hit on store creation. Seems worth it to avoid the duplication of code / effort.

            var contractMap = await Task.Run(() => 
                EnumerateFindStates(cachingClient, stateRootHash, NativeContract.ContractManagement.Hash, prefix)
                    .Where(kvp => kvp.key.AsSpan().StartsWith(prefix.Span))
                    .Select(kvp => new StorageItem(kvp.value).GetInteroperable<ContractState>())
                    .ToImmutableDictionary(c => c.Id, c => (c.Hash, c.Manifest))).ConfigureAwait(false);

            return new StateServiceStore(cachingClient, index, stateRootHash, settings, contractMap);
        }

        public void Dispose()
        {
            cachingClient.Dispose();
            GC.SuppressFinalize(this);
        }

        public byte[]? TryGet(byte[] key)
        {
            var contractId = BinaryPrimitives.ReadInt32LittleEndian(key.AsSpan(0, 4));

            // Since blocks and transactions are immutable and already available via other JSON-RPC
            // methods, the state service does not store ledger contract data. For the StateServiceStore,
            // we need to translate TryGet calls into equivalent non-state service calls.

            if (contractId == NativeContract.Ledger.Id)
            {
                return key[4] switch
                {
                    Ledger_Prefix_CurrentBlock => Serialize(cachingClient.GetBlockHash(index)),
                    Ledger_Prefix_BlockHash
                        or Ledger_Prefix_Block
                        or Ledger_Prefix_Transaction => cachingClient.GetLedgerStorage(key.AsMemory(4)),
                    _ => throw new NotSupportedException()
                };
            }

            // for all other contracts, we simply need to translate the contract ID into the contract
            // hash and call the State Service GetState method.

            if (contractMap.TryGetValue(contractId, out var contract))
            {
                return cachingClient.GetState(rootHash, contract.hash, key.AsMemory(4));
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
                return Seek(contract.hash, contractId, key.AsMemory(4), direction);
            }

            throw new InvalidOperationException($"Invalid contract ID {contractId}");
        }

        IEnumerable<(byte[] Key, byte[] Value)> Seek(UInt160 contractHash, int contractId, ReadOnlyMemory<byte> prefix, SeekDirection direction)
        {
            var comparer = direction == SeekDirection.Forward
                ? MemorySequenceComparer.Default
                : MemorySequenceComparer.Reverse;

            return EnumerateFindStates(cachingClient, rootHash, contractHash, prefix)
                .Select(kvp =>
                {
                    var k = new byte[kvp.key.Length + 4];
                    BinaryPrimitives.WriteInt32LittleEndian(k.AsSpan(0, 4), contractId);
                    kvp.key.CopyTo(k.AsSpan(4));
                    return (key: k, kvp.value);
                })
                .OrderBy(kvp => kvp.key, comparer);
        }

        byte[] Serialize(UInt256 value)
        {
            var @struct = new VM.Types.Struct() { value.ToArray(), index };
            return BinarySerializer.Serialize(@struct, 1024*1024);
        }

        static async Task<ProtocolSettings> GetSettingsAsync(Uri uri) 
        {
            using var rpcClient = new RpcClient(uri);
            var version = await rpcClient.GetVersionAsync().ConfigureAwait(false);
            return ProtocolSettings.Default with
            {
                AddressVersion = version.Protocol.AddressVersion,
                Network = version.Protocol.Network
            };
        }

        static IEnumerable<(byte[] key, byte[] value)> EnumerateFindStates(ICachingClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, int? pageSize = null)
        {
            var from = Array.Empty<byte>();
            while (true)
            {
                var foundStates = rpcClient.FindStates(rootHash, scriptHash, prefix, from, pageSize);
                var states = foundStates.Results;
                for (int i = 0; i < states.Length; i++)
                {
                    yield return (states[i].key, states[i].value);
                }
                if (!foundStates.Truncated || states.Length == 0) break;
                from = states[states.Length - 1].key;
            }
        }
    }
}
