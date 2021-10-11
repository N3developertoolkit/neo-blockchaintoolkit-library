using System;
using System.Collections.Generic;
using Neo.IO.Json;

namespace Neo.BlockchainToolkit.Persistence.RPC
{

    static class RpcClientExtensions
    {
        public static RpcVersion GetVersion(this SyncRpcClient rpcClient)
        {
            var result = rpcClient.RpcSend(Neo.Network.RPC.RpcClient.GetRpcName());
            return RpcVersion.FromJson(result);
        }

        public static RpcStateRoot GetStateRoot(this SyncRpcClient rpcClient, uint index)
        {
            var result = rpcClient.RpcSend(Neo.Network.RPC.RpcClient.GetRpcName(), index);
            return RpcStateRoot.FromJson(result);
        }

        public static byte[] GetState(this SyncRpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
        {
            var result = rpcClient.RpcSend(Neo.Network.RPC.RpcClient.GetRpcName(),
                rootHash.ToString(), scriptHash.ToString(), Convert.ToBase64String(key));
            return Convert.FromBase64String(result.AsString());
        }

        public static IEnumerable<(byte[] key, byte[] value)> EnumerateFindStates(this SyncRpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, int? pageSize = null)
        {
            var from = Array.Empty<byte>();
            while (true)
            {
                var foundStates = rpcClient.FindStates(rootHash, scriptHash, prefix.Span, from, pageSize);
                var states = foundStates.Results;
                for (int i = 0; i < states.Length; i++)
                {
                    yield return (states[i].key, states[i].value);
                }
                if (!foundStates.Truncated || states.Length == 0) break;
                from = states[states.Length - 1].key;
            }
        }

        public static RpcFoundStates FindStates(this SyncRpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> from = default, int? count = null)
        {
            var @params = MakeFindStatesParams(rootHash, scriptHash, prefix, from, count);
            var result = rpcClient.RpcSend(Neo.Network.RPC.RpcClient.GetRpcName(), @params);
            return RpcFoundStates.FromJson(result);
        }

        static JObject[] MakeFindStatesParams(UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> from = default, int? count = null)
        {
            var paramCount = from.Length == 0 ? 3 : count == null ? 4 : 5;
            var @params = new JObject[paramCount];
            @params[0] = rootHash.ToString();
            @params[1] = scriptHash.ToString();
            @params[2] = Convert.ToBase64String(prefix);
            if (from.Length > 0)
            {
                @params[3] = Convert.ToBase64String(from);
                if (count.HasValue)
                {
                    @params[4] = count.Value;
                }
            }
            return @params;
        }
    }
}
