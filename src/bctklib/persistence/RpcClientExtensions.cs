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

        public static byte[]? GetProof(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
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
            var proof = rpcClient.GetProof(rootHash, scriptHash, key);
            return proof is null ? null : VerifyProof(rootHash, proof);
        }

        public static RpcFoundStates FindStates(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> from = default, int? count = null)
        {
            var @params = StateAPI.MakeFindStatesParams(rootHash, scriptHash, prefix, from, count);
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), @params);
            var foundStates = RpcFoundStates.FromJson(result);
            var first = VerifyProof(rootHash, foundStates.FirstProof);
            var last = VerifyProof(rootHash, foundStates.LastProof);
            return foundStates;
        }

        static byte[] VerifyProof(UInt256 rootHash, byte[] proof)
        {
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
            var storageItem = Trie<StorageKey, StorageItem>.VerifyProof(rootHash, storageKey, proofs);
            if (storageItem is null) throw new Exception("Verification failed");
            return storageItem.Value;
        }
    }
}
