using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo;
using Neo.BlockchainToolkit.Persistence.RPC;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore
    {
        class MemoryCacheClient : ICachingClient
        {
            readonly SyncRpcClient rpcClient;

            readonly Lazy<RpcVersion> version;
            LockingDictionary<uint, RpcStateRoot> stateRoots = new();
            LockingDictionary<uint, UInt256> blockHashes = new();
            LockingDictionary<int, RpcFoundStates> foundStates = new();
            LockingDictionary<int, byte[]?> retrievedStates = new();
            LockingDictionary<int, byte[]> storages = new();

            public MemoryCacheClient(SyncRpcClient rpcClient)
            {
                this.rpcClient = rpcClient;
                version = new Lazy<RpcVersion>(() => rpcClient.GetVersion());
            }

            public void Dispose()
            {
                rpcClient.Dispose();
            }

            public RpcFoundStates FindStates(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default, int? count = null)
            {
                var hash = HashCode.Combine(
                    rootHash,
                    scriptHash,
                    ReadOnlyMemoryComparer.GetHashCode(prefix.Span),
                    ReadOnlyMemoryComparer.GetHashCode(from.Span),
                    count);
                return foundStates.GetOrAdd(hash,
                    _ => rpcClient.FindStates(rootHash, scriptHash, prefix.Span, from.Span, count));
            }

            public UInt256 GetBlockHash(uint index)
            {
                return blockHashes.GetOrAdd(index, i => rpcClient.GetBlockHash(i));
            }

            public byte[]? GetState(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> key)
            {
                var doo = new Dictionary<int, RpcFoundStates>();

                var hash = HashCode.Combine(rootHash, scriptHash, ReadOnlyMemoryComparer.GetHashCode(key.Span));
                return retrievedStates.GetOrAdd(hash, _ => rpcClient.GetState(rootHash, scriptHash, key.Span));
            }

            public RpcStateRoot GetStateRoot(uint index)
            {
                return stateRoots.GetOrAdd(index, i => rpcClient.GetStateRoot(i));
            }

            public byte[] GetStorage(UInt160 contractHash, ReadOnlyMemory<byte> key)
            {
                var hash = HashCode.Combine(contractHash, ReadOnlyMemoryComparer.GetHashCode(key.Span));
                return storages.GetOrAdd(hash, _ => rpcClient.GetStorage(contractHash, key.Span));
            }

            public RpcVersion GetVersion()
            {
                return version.Value;
            }
        }
    }
}
