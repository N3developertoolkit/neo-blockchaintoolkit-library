using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit.Persistence.RPC;
using Neo.IO;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore : IReadOnlyStore, IDisposable
    {
        interface ICache : IDisposable
        {
            bool TryGet(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[] value);
            IEnumerable<(byte[] key, byte[] value)> Seek(UInt160 contractHash, ReadOnlyMemory<byte> prefix, SeekDirection direction);
        }

        readonly SyncRpcClient rpcClient;
        readonly uint index;
        readonly UInt256 rootHash;
        // readonly ICache cache;
        readonly IReadOnlyDictionary<int, (UInt160 hash, ContractManifest manifest)> contractMap;

        public StateServiceStore(string uri, uint index, string? cachePath = null)
            : this(new Uri(uri), index, cachePath)
        {
        }

        public StateServiceStore(Uri uri, uint index, string? cachePath = null)
            : this(new SyncRpcClient(uri), index, cachePath)
        {
        }

        public StateServiceStore(SyncRpcClient rpcClient, uint index, string? cachePath = null)
        {
            this.rpcClient = rpcClient;
            this.index = index;
            this.rootHash = rpcClient.GetStateRoot(index).RootHash;

            // this.cache = string.IsNullOrEmpty(cachePath)
            //     ? new MemoryCache(EnumerateContractStates)
            //     : new RocksDbCache(EnumerateContractStates, cachePath);

            using var memoryOwner = MemoryPool<byte>.Shared.Rent(1);
            memoryOwner.Memory.Span[0] = 0x08; // ContractManagement.Prefix_Contract == 8
            var prefix = memoryOwner.Memory.Slice(0, 1);

            contractMap = rpcClient
                .EnumerateFindStates(rootHash, NativeContract.ContractManagement.Hash, prefix)
                .Where(kvp => kvp.key.AsSpan().StartsWith(prefix.Span))
                .Select(kvp => new StorageItem(kvp.value).GetInteroperable<ContractState>())
                .ToImmutableDictionary(c => c.Id, c => (c.Hash, c.Manifest));
        }

        public void Dispose()
        {
            rpcClient.Dispose();
            // cache.Dispose();
        }

        public RpcVersion GetVersion() => rpcClient.GetVersion();

        const byte Ledger_Prefix_BlockHash = 9;
        const byte Ledger_Prefix_CurrentBlock = 12;
        const byte Ledger_Prefix_Block = 5;
        const byte Ledger_Prefix_Transaction = 11;
        const byte NeoToken_Prefix_GasPerBlock = 29;
        const byte NeoToken_Prefix_VoterRewardPerCommittee = 23;

        public byte[]? TryGet(byte[] key)
        {
            var contractId = BinaryPrimitives.ReadInt32LittleEndian(key.AsSpan(0, 4));

            if (contractId == NativeContract.Ledger.Id)
            {
                // TODO: integrate this into the cache
                return key[4] switch
                {
                    Ledger_Prefix_CurrentBlock => GetLedgerCurrentBlock(rpcClient, index),
                    Ledger_Prefix_BlockHash => rpcClient.GetStorage(NativeContract.Ledger.Hash, key.AsSpan(4)),
                    Ledger_Prefix_Block => rpcClient.GetStorage(NativeContract.Ledger.Hash, key.AsSpan(4)),
                    Ledger_Prefix_Transaction => rpcClient.GetStorage(NativeContract.Ledger.Hash, key.AsSpan(4)),
                    _ => throw new NotSupportedException()
                };
            }

            if (contractMap.TryGetValue(contractId, out var contract))
            {
                return rpcClient.GetState(rootHash, contract.hash, key.AsSpan(4));
            }

            throw new InvalidOperationException($"Invalid contract ID {contractId}");

            static byte[] GetLedgerCurrentBlock(SyncRpcClient rpcClient, uint index)
            {
                var blockHash = rpcClient.GetBlockHash(index);
                var hashIndexState = new VM.Types.Struct() { blockHash.ToArray(), index };
                return BinarySerializer.Serialize(hashIndexState, 1024 * 1024);
            }
        }

        public bool Contains(byte[] key) => TryGet(key) == null;

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] key, SeekDirection direction)
        {
            var contractId = BinaryPrimitives.ReadInt32LittleEndian(key.AsSpan(0, 4));
            if (contractId < 0)
            {
                if (contractId == NativeContract.Ledger.Id)
                {
                    // The only place the native Ledger contract uses seek is in the Initialized
                    // method, and it's only checking for the existence of any value with  
                    // Prefix_Block value. So return a single empty value in this case so the .Any()
                    // method returns true

                    if (key[4] == Ledger_Prefix_Block)
                    {
                        Debug.Assert(key.Length == 5);
                        return Enumerable.Repeat((key, Array.Empty<byte>()), 1);
                    }

                    throw new NotSupportedException("Native Ledger contract does not support Seek");
                }

                if (contractId == NativeContract.NEO.Id)
                {
                    if (key[4] == NeoToken_Prefix_VoterRewardPerCommittee)
                    {
                        // VoterRewardPerCommittee keys are searched by voter ECPoint (33 bytes long)
                        // so use 1 byte VoterRewardPerCommittee prefix + 33 bytes voter ECPoint for seek

                        if (key.Length < 38) throw new InvalidOperationException("Invalid NeoToken VoterRewardPerCommittee key");
                        return Seek(NativeContract.NEO.Hash, contractId, key.AsMemory(4, 34), direction);
                    }

                    if (key[4] == NeoToken_Prefix_GasPerBlock)
                    {
                        // GasPerBlock keys are only searched via the GasPerBlock prefix

                        return Seek(NativeContract.NEO.Hash, contractId, key.AsMemory(4, 1), direction);
                    }
                }

                if (contractId == NativeContract.RoleManagement.Id)
                {
                    // RoleManagement keys are only searched by Role prefix

                    Debug.Assert(Enum.IsDefined<Role>((Role)key[4]));
                    return Seek(NativeContract.NEO.Hash, contractId, key.AsMemory(4, 1), direction);
                }
            }

            if (contractMap.TryGetValue(contractId, out var contract))
            {
                // remaining native contract search + all VM storage search treat the key as a prefix

                return Seek(contract.hash, contractId, key.AsMemory(4), direction);
            }

            throw new InvalidOperationException($"Invalid contract ID {contractId}");
        }

        IEnumerable<(byte[] Key, byte[] Value)> Seek(UInt160 contractHash, int contractId, ReadOnlyMemory<byte> prefix, SeekDirection direction)
        {
            var comparer = direction == SeekDirection.Forward
                ? ReadOnlyMemoryComparer.Default
                : ReadOnlyMemoryComparer.Reverse;

            return rpcClient
                .EnumerateFindStates(rootHash, contractHash, prefix)
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
