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

        public static byte[]? GetProvenState(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
        {
            try
            {
                var result = rpcClient.RpcSend("getproof",
                    rootHash.ToString(),
                    scriptHash.ToString(),
                    Convert.ToBase64String(key));
                var proof = Convert.FromBase64String(result.AsString());
                return VerifyProof(proof, rootHash).item.Value;
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
                // Unfortunately, StateService GetProof method throws a custom exception 
                // instead of KeyNotFoundException like GetState. 
                // https://github.com/neo-project/neo-modules/pull/706 tracks changing
                // exception thrown by GetProof. 
                // Until this is changed, also look for the the custom exception thrown
                // by GetProof

                return null;
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
                var (storageKey, storageItem) = VerifyProof(proof, rootHash);
                if (!result.key.AsSpan().SequenceEqual(storageKey.Key)) throw new Exception("Incorrect StorageKey");
                if (!result.value.AsSpan().SequenceEqual(storageItem.Value)) throw new Exception("Incorrect StorageItem");
            }
        }

        static (StorageKey key, StorageItem item) VerifyProof(byte[]? proof, UInt256 rootHash)
        {
            ArgumentNullException.ThrowIfNull(proof);

            var proofs = new HashSet<byte[]>();

            using MemoryStream stream = new(proof, false);
            using BinaryReader reader = new(stream, Utility.StrictUTF8);

            var key = reader.ReadVarBytes(Node.MaxKeyLength);
            var count = reader.ReadVarInt();
            for (ulong i = 0; i < count; i++)
            {
                proofs.Add(reader.ReadVarBytes());
            }

            var storageKey = key.AsSerializable<StorageKey>();
            if (storageKey is null) throw new Exception($"Invalid {nameof(StorageKey)}");
            var storageItem = Trie<StorageKey, StorageItem>.VerifyProof(rootHash, storageKey, proofs);
            if (storageItem is null) throw new Exception("Verification failed");

            return (storageKey, storageItem);
        }
    }
}
