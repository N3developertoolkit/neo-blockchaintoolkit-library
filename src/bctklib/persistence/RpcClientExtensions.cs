using System;
using System.Threading.Tasks;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;

namespace Neo.BlockchainToolkit.Persistence
{
    static class RpcClientExtensions
    {
        // public static RpcVersion GetVersion(this RpcClient rpcClient)
        // {
        //     var result = rpcClient.RpcSend(RpcClient.GetRpcName());
        //     return RpcVersion.FromJson((Json.JObject)result);
        // }

        // public static UInt256 GetBlockHash(this RpcClient rpcClient, uint index)
        // {
        //     var result = rpcClient.RpcSend(RpcClient.GetRpcName(), index);
        //     return UInt256.Parse(result.AsString());
        // }


        // TODO: remove when https://github.com/neo-project/neo-modules/issues/756 is resolved
        internal static async Task<UInt256> GetBlockHashAsync(this RpcClient rpcClient, uint index)
        {
            var result = await rpcClient.RpcSendAsync("getblockhash", index).ConfigureAwait(false);
            return UInt256.Parse(result.AsString());
        }


        internal static byte[] GetProof(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
        {
            var result = rpcClient.RpcSend(
                RpcClient.GetRpcName(),
                rootHash.ToString(),
                scriptHash.ToString(),
                Convert.ToBase64String(key));
            return Convert.FromBase64String(result.AsString());
        }

        internal static byte[] GetStorage(this RpcClient rpcClient, UInt160 contractHash, ReadOnlySpan<byte> key)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), contractHash.ToString(), Convert.ToBase64String(key));
            return Convert.FromBase64String(result.AsString());
        }

        internal static RpcStateRoot GetStateRoot(this RpcClient rpcClient, uint index)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), index);
            return RpcStateRoot.FromJson((Json.JObject)result);
        }

        internal static async Task<RpcStateRoot> GetStateRootAsync(this RpcClient rpcClient, uint index)
        {
            var result = await rpcClient.RpcSendAsync(RpcClient.GetRpcName(), index).ConfigureAwait(false);
            return RpcStateRoot.FromJson((Json.JObject)result);
        }

        internal static RpcFoundStates FindStates(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> from = default, int? count = null)
        {
            var @params = StateAPI.MakeFindStatesParams(rootHash, scriptHash, prefix, from, count);
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), @params);
            return RpcFoundStates.FromJson((Json.JObject)result);
        }

        internal static async Task<RpcFoundStates> FindStatesAsync(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default, int? count = null)
        {
            var @params = StateAPI.MakeFindStatesParams(rootHash, scriptHash, prefix.Span, from.Span, count);
            var result = await rpcClient.RpcSendAsync(RpcClient.GetRpcName(), @params).ConfigureAwait(false);
            return RpcFoundStates.FromJson((Json.JObject)result);
        }

        // internal static IEnumerable<(byte[] key, byte[] value)> EnumerateStates(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, int? pageSize = null)
        // {
        //     var from = Array.Empty<byte>();
        //     while (true)
        //     {
        //         var foundStates = rpcClient.FindStates(rootHash, scriptHash, prefix.Span, from, pageSize);
        //         var states = ValidateFoundStates(rootHash, foundStates);
        //         for (int i = 0; i < states.Length; i++)
        //         {
        //             yield return (states[i].key, states[i].value);
        //         }
        //         if (!foundStates.Truncated || states.Length == 0) break;
        //         from = states[^1].key;
        //     }
        // }

        // internal static IEnumerable<(byte[] key, byte[] value)> EnumerateStates(this StateServiceStore.ICachingClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, int? pageSize = null)
        // {
        //     var from = Array.Empty<byte>();
        //     while (true)
        //     {
        //         var foundStates = rpcClient.FindStates(rootHash, scriptHash, prefix, from, pageSize);
        //         var states = ValidateFoundStates(rootHash, foundStates);
        //         for (int i = 0; i < states.Length; i++)
        //         {
        //             yield return (states[i].key, states[i].value);
        //         }
        //         if (!foundStates.Truncated || states.Length == 0) break;
        //         from = states[^1].key;
        //     }
        // }


        // internal static async IAsyncEnumerable<(byte[] key, byte[] value)> EnumerateStatesAsync(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, int? pageSize = null)
        // {
        //     var from = Array.Empty<byte>();
        //     while (true)
        //     {
        //         var foundStates = await rpcClient.FindStatesAsync(rootHash, scriptHash, prefix, from.AsMemory(), pageSize).ConfigureAwait(false);
        //         var states = ValidateFoundStates(rootHash, foundStates);
        //         for (int i = 0; i < states.Length; i++)
        //         {
        //             yield return (states[i].key, states[i].value);
        //         }
        //         if (!foundStates.Truncated || states.Length == 0) break;
        //         from = states[^1].key;
        //     }
        // }

        // internal static (byte[] key, byte[] value)[] ValidateFoundStates(UInt256 rootHash, Network.RPC.Models.RpcFoundStates foundStates)
        // {
        //     if (foundStates.Results.Length > 0)
        //     {
        //         ValidateProof(rootHash, foundStates.FirstProof, foundStates.Results[0]);
        //     }
        //     if (foundStates.Results.Length > 1)
        //     {
        //         ValidateProof(rootHash, foundStates.LastProof, foundStates.Results[^1]);
        //     }
        //     return foundStates.Results;

        //     static void ValidateProof(UInt256 rootHash, byte[]? proof, (byte[] key, byte[] value) result)
        //     {
        //         var (storageKey, storageValue) = Utility.VerifyProof(rootHash, proof);
        //         if (!result.key.AsSpan().SequenceEqual(storageKey.Key.Span)) throw new Exception("Incorrect StorageKey");
        //         if (!result.value.AsSpan().SequenceEqual(storageValue)) throw new Exception("Incorrect StorageItem");
        //     }
        // }
    }
}
