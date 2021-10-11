using System;
using System.Collections.Generic;
using Neo.IO.Json;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;

namespace Neo.BlockchainToolkit.Persistence.RPC
{

    static class RpcClientExtensions
    {
        public static RpcVersion GetVersion(this SyncRpcClient rpcClient)
        {
            var result = rpcClient.RpcSend(Neo.Network.RPC.RpcClient.GetRpcName());
            return RpcVersion.FromJson(result);
        }

        public static UInt256 GetBlockHash(this SyncRpcClient rpcClient, uint index)
        {
            var result = rpcClient.RpcSend(Neo.Network.RPC.RpcClient.GetRpcName(), index);
            return UInt256.Parse(result.AsString());
        }

        public static byte[] GetStorage(this SyncRpcClient rpcClient, UInt160 contractHash, ReadOnlySpan<byte> key)
        {
            var result = rpcClient.RpcSend(Neo.Network.RPC.RpcClient.GetRpcName(), contractHash.ToString(), Convert.ToBase64String(key));
            return Convert.FromBase64String(result.AsString());
        }

        public static RpcStateRoot GetStateRoot(this SyncRpcClient rpcClient, uint index)
        {
            var result = rpcClient.RpcSend(Neo.Network.RPC.RpcClient.GetRpcName(), index);
            return RpcStateRoot.FromJson(result);
        }

        public static byte[]? GetState(this SyncRpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
        {
            var request = SyncRpcClient.AsRpcRequest(Neo.Network.RPC.RpcClient.GetRpcName(),
                rootHash.ToString(), scriptHash.ToString(), Convert.ToBase64String(key));
            var response = rpcClient.Send(request);
            return response.AsStateResponse();
        }

        public static byte[]? AsStateResponse(this RpcResponse response)
        {
            if (response.Error != null)
            {
                const int COR_E_KEYNOTFOUND = unchecked((int)0x80131577);
                if (response.Error.Code == COR_E_KEYNOTFOUND)
                {
                    return null;
                }
                else
                {
                    throw new RpcException(response.Error.Code, response.Error.Message);
                }
            }
            else
            {
                return Convert.FromBase64String(response.Result.AsString());
            }
        }

        public static RpcFoundStates FindStates(this SyncRpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> from = default, int? count = null)
        {
            var @params = MakeFindStatesParams(rootHash, scriptHash, prefix, from, count);
            var result = rpcClient.RpcSend(Neo.Network.RPC.RpcClient.GetRpcName(), @params);
            return RpcFoundStates.FromJson(result);
        }

        public static JObject[] MakeFindStatesParams(UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> from = default, int? count = null)
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
