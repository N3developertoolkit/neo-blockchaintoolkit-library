using System;
using System.Collections.Concurrent;
using Neo.Network.RPC;
using Neo.SmartContract.Native;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore
    {
        internal class MemoryCacheClient : ICachingClient
        {
            readonly RpcClient rpcClient;
            readonly UInt256 rootHash;
            readonly bool shared;
            readonly ConcurrentDictionary<int, FoundStates> foundStates = new();
            readonly ConcurrentDictionary<int, byte[]?> proofs = new();
            readonly ConcurrentDictionary<int, byte[]> storages = new();
            bool disposed = false;

            public MemoryCacheClient(RpcClient rpcClient, UInt256 rootHash, bool shared = false)
            {
                this.rpcClient = rpcClient;
                this.rootHash = rootHash;
                this.shared = shared;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (!shared)
                {
                    rpcClient.Dispose();
                    GC.SuppressFinalize(this);
                }
            }

            public FoundStates FindStates(UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default)
            {
                if (disposed) throw new ObjectDisposedException(nameof(MemoryCacheClient));
                var hash = HashCode.Combine(
                    scriptHash,
                    MemorySequenceComparer.GetHashCode(prefix.Span),
                    MemorySequenceComparer.GetHashCode(from.Span));
                return foundStates.GetOrAdd(hash, _ => FindProvenStates(rpcClient, rootHash, scriptHash, prefix.Span, from.Span));
            }

            public byte[]? GetContractState(UInt160 scriptHash, ReadOnlyMemory<byte> key)
            {
                if (disposed) throw new ObjectDisposedException(nameof(MemoryCacheClient));
                var hash = HashCode.Combine(
                    scriptHash,
                    MemorySequenceComparer.GetHashCode(key.Span));
                return proofs.GetOrAdd(hash, _ => GetProvenState(rpcClient, rootHash, scriptHash, key.Span));
            }

            public byte[] GetLedgerStorage(ReadOnlyMemory<byte> key)
            {
                if (disposed) throw new ObjectDisposedException(nameof(MemoryCacheClient));
                var hash = MemorySequenceComparer.GetHashCode(key.Span);
                return storages.GetOrAdd(hash, _ => rpcClient.GetStorage(NativeContract.Ledger.Hash, key.Span));
            }
        }
    }
}
