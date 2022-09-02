using System;
using System.Collections.Concurrent;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore
    {
        internal class MemoryCacheClient : ICachingClient
        {
            readonly RpcClient rpcClient;
            readonly ConcurrentDictionary<int, RpcFoundStates> foundStates = new();
            readonly ConcurrentDictionary<int, byte[]?> proofs = new();
            readonly ConcurrentDictionary<int, byte[]> storages = new();
            bool disposed = false;

            public MemoryCacheClient(RpcClient rpcClient)
            {
                this.rpcClient = rpcClient;
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    rpcClient.Dispose();
                    disposed = true;
                }
            }

            public RpcFoundStates FindStates(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default, int? count = null)
            {
                if (disposed) throw new ObjectDisposedException(nameof(MemoryCacheClient));
                var hash = HashCode.Combine(
                    rootHash,
                    scriptHash,
                    MemorySequenceComparer.GetHashCode(prefix.Span),
                    MemorySequenceComparer.GetHashCode(from.Span),
                    count);
                return foundStates.GetOrAdd(hash,
                    _ => rpcClient.FindStates(rootHash, scriptHash, prefix.Span, from.Span, count));
            }

            public byte[]? GetProvenState(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> key)
            {
                if (disposed) throw new ObjectDisposedException(nameof(MemoryCacheClient));
                var hash = HashCode.Combine(
                    rootHash,
                    scriptHash,
                    MemorySequenceComparer.GetHashCode(key.Span));
                return proofs.GetOrAdd(hash, _ => rpcClient.GetProvenState(rootHash, scriptHash, key.Span));
            }

            public byte[] GetStorage(UInt160 contractHash, ReadOnlyMemory<byte> key)
            {
                if (disposed) throw new ObjectDisposedException(nameof(MemoryCacheClient));
                var hash = HashCode.Combine(
                    contractHash,
                    MemorySequenceComparer.GetHashCode(key.Span));
                return storages.GetOrAdd(hash, _ => rpcClient.GetStorage(contractHash, key.Span));
            }
        }
    }
}
