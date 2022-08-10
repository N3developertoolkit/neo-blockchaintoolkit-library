using System;
using System.Collections.Generic;
using System.IO;
using Neo.Cryptography.MPTTrie;
using Neo.IO;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract;

namespace Neo.BlockchainToolkit.Persistence
{
    static class RpcClientExtensions
    {
        internal const int COR_E_KEYNOTFOUND = unchecked((int)0x80131577);

        public static RpcVersion GetVersion(this RpcClient rpcClient)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName());
            return RpcVersion.FromJson((Json.JObject)result);
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
            return RpcStateRoot.FromJson((Json.JObject)result);
        }

        public static byte[]? GetProvenState(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
        {
            try
            {
                var result = rpcClient.RpcSend("getproof",
                    rootHash.ToString(),
                    scriptHash.ToString(),
                    Convert.ToBase64String(key));
                var proof = Convert.FromBase64String(result.AsString());
                return Utility.VerifyProof(rootHash, proof).value;
            }
            // GetProvenState has to match the semantics of IReadOnlyStore.TryGet
            // which returns null for invalid keys instead of throwing an exception.
            catch (RpcException ex) when (ex.HResult == COR_E_KEYNOTFOUND)
            {
                // Trie class throws KeyNotFoundException if key is not in the trie.
                // RpcClient/Server converts the KeyNotFoundException into an
                // RpcException with code == COR_E_KEYNOTFOUND.

                return null;
            }
            catch (RpcException ex) when (ex.HResult == -100 && ex.Message == "Unknown value")
            {
                // Prior to Neo 3.3.0, StateService GetProof method threw a custom exception 
                // instead of KeyNotFoundException like GetState. This catch clause detected
                // the custom exception that GetProof used to throw. 

                // TODO: remove this clause once deployed StateService for Neo N3 MainNet and
                //       TestNet has been verified to be running Neo 3.3.0 or later.

                return null;
            }
        }

        public static RpcFoundStates FindStates(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> from = default, int? count = null)
        {
            var @params = StateAPI.MakeFindStatesParams(rootHash, scriptHash, prefix, from, count);
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), @params);
            var foundStates = RpcFoundStates.FromJson((Json.JObject)result);
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
                var (storageKey, storageValue) = Utility.VerifyProof(rootHash, proof);
                if (!result.key.AsSpan().SequenceEqual(storageKey.Key.Span)) throw new Exception("Incorrect StorageKey");
                if (!result.value.AsSpan().SequenceEqual(storageValue)) throw new Exception("Incorrect StorageItem");
            }
        }
    }
}
