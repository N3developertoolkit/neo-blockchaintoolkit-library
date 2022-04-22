
using System;
using System.Collections.Generic;
using System.IO;
using Neo.Cryptography.MPTTrie;
using Neo.IO;
using Neo.SmartContract;

namespace Neo.BlockchainToolkit
{
    public static class Utility
    {
        public static (StorageKey key, StorageItem item) VerifyProof(byte[]? proof, UInt256 rootHash)
        {
            ArgumentNullException.ThrowIfNull(proof);

            var proofs = new HashSet<byte[]>();

            using MemoryStream stream = new(proof, false);
            using BinaryReader reader = new(stream, Neo.Utility.StrictUTF8);

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
