using System;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;

namespace Neo.BlockchainToolkit.Persistence
{
    static class RpcClientExtensions
    {
        internal const int COR_E_KEYNOTFOUND = unchecked((int)0x80131577);

        public static RpcVersion GetVersion(this RpcClient rpcClient)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName());
            return RpcVersion.FromJson(result);
        }

        public static UInt256 GetBlockHash(this RpcClient rpcClient, uint index)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), index);
            return UInt256.Parse(result.AsString());
        }

        public static byte[] GetStorage(this RpcClient rpcClient, UInt160 contractHash, ReadOnlySpan<byte> key)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), contractHash.ToString(), Convert.ToBase64String(key));
            return Convert.FromBase64String(result.AsString());
        }

        public static RpcStateRoot GetStateRoot(this RpcClient rpcClient, uint index)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), index);
            return RpcStateRoot.FromJson(result);
        }

        static byte[]? GetProof(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
        {
            try
            {
                var result = rpcClient.RpcSend(RpcClient.GetRpcName(),
                    rootHash.ToString(), scriptHash.ToString(), Convert.ToBase64String(key));
                return Convert.FromBase64String(result.AsString());
            }
            catch (RpcException ex)
            {
                if (ex.HResult == COR_E_KEYNOTFOUND) return null;
                throw;
            }
        }

        public static byte[]? GetProvenState(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
        {
            try
            {
                var result = rpcClient.RpcSend("getproof",
                    rootHash.ToString(), scriptHash.ToString(), Convert.ToBase64String(key));
                var proof = Convert.FromBase64String(result.AsString());
                return proof.VerifyProof(rootHash).item.Value;
            }
            catch (RpcException ex)
            {
                if (ex.HResult == COR_E_KEYNOTFOUND) return null;
                throw;
            }
        }

        public static RpcFoundStates FindStates(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> from = default, int? count = null)
        {
            var @params = StateAPI.MakeFindStatesParams(rootHash, scriptHash, prefix, from, count);
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), @params);
            var foundStates = RpcFoundStates.FromJson(result);
            if (foundStates.Results.Length > 0)
            {
                ValidateProof(rootHash, foundStates.FirstProof, foundStates.Results[0]);
            }
            if (foundStates.Results.Length > 1)
            {
                ValidateProof(rootHash, foundStates.LastProof, foundStates.Results[^1]);
            }
            return foundStates;

            static void ValidateProof(UInt256 rootHash, byte[]? proof, (byte[] key, byte[] value) result)
            {
                var (storageKey, storageItem) = proof.VerifyProof(rootHash);
                if (!result.key.AsSpan().SequenceEqual(storageKey.Key)) throw new Exception("Incorrect StorageKey");
                if (!result.value.AsSpan().SequenceEqual(storageItem.Value)) throw new Exception("Incorrect StorageItem");
            }
        }
    }
}
