using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        readonly ICache cache;
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
            this.cache = string.IsNullOrEmpty(cachePath)
                ? new MemoryCache(EnumerateContractStates)
                : new RocksDbCache(EnumerateContractStates, cachePath);

            using var memoryOwner = MemoryPool<byte>.Shared.Rent(1);
            memoryOwner.Memory.Span[0] = 0x08; // ContractManagement.Prefix_Contract == 8
            var prefix = memoryOwner.Memory.Slice(0, 1);
            contractMap = cache.Seek(NativeContract.ContractManagement.Hash, prefix, SeekDirection.Forward)
                .Where(kvp => kvp.key.AsSpan().StartsWith(prefix.Span))
                .Select(kvp => new StorageItem(kvp.value).GetInteroperable<ContractState>())
                .ToImmutableDictionary(c => c.Id, c => (c.Hash, c.Manifest));
        }

        public void Dispose()
        {
            rpcClient.Dispose();
            cache.Dispose();
        }

        public RpcVersion GetVersion()
        {
            return rpcClient.GetVersion();
        }

        IEnumerable<(byte[] key, byte[] value)> EnumerateContractStates(UInt160 contractHash)
        {
            return rpcClient.EnumerateFindStates(rootHash, contractHash, default);
        }

        const byte Ledger_Prefix_BlockHash = 9;
        const byte Ledger_Prefix_CurrentBlock = 12;
        const byte Ledger_Prefix_Block = 5;
        const byte Ledger_Prefix_Transaction = 11;

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

            if (contractMap.TryGetValue(contractId, out var contract)
                && cache.TryGet(contract.hash, key.AsMemory(4), out var value))
            {
                return value;
            }

            return null;

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
            if (contractId == NativeContract.Ledger.Id)
            {
                // The only place the native Ledger contract uses seek is in the Initialized
                // method, and it's only checking for the existence of any value with  
                // Prefix_Block value. So return a single empty value in this case so the .Any()
                // method returns true

                if (key[4] == Ledger_Prefix_Block && key.Length == 5)
                {
                    return Enumerable.Repeat((key, Array.Empty<byte>()), 1);
                }

                throw new NotSupportedException("Native Ledger contract does not support Seek");
            }

            if (contractMap.TryGetValue(contractId, out var contract))
            {
                return cache.Seek(contract.hash, key.AsMemory(4), direction)
                    .Select(kvp =>
                    {
                        var k = new byte[kvp.key.Length + 4];
                        BinaryPrimitives.WriteInt32LittleEndian(k.AsSpan(0, 4), contractId);
                        kvp.key.CopyTo(k.AsSpan(4));
                        return (k, kvp.value);
                    });
            }
            else
            {
                return Enumerable.Empty<(byte[], byte[])>();
            }
        }
    }
}
