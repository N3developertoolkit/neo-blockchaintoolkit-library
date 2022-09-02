using System;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore
    {
        class NoCacheClient : ICachingClient
        {
            readonly RpcClient rpcClient;

            public NoCacheClient(RpcClient rpcClient)
            {
                this.rpcClient = rpcClient;
            }

            public void Dispose()
            {
                rpcClient.Dispose();
            }

            public RpcFoundStates FindStates(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default, int? count = null)
            {
                return rpcClient.FindStates(rootHash, scriptHash, prefix.Span, from.Span, count);
            }

            public byte[]? GetProvenState(UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> key)
            {
                return rpcClient.GetProvenState(rootHash, scriptHash, key.Span);
            }

            public byte[] GetStorage(UInt160 contractHash, ReadOnlyMemory<byte> key)
            {
                return rpcClient.GetStorage(contractHash, key.Span);
            }
        }
    }
}
