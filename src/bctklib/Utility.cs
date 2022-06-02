
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Neo.Cryptography.MPTTrie;
using Neo.IO;
using Neo.SmartContract;

namespace Neo.BlockchainToolkit
{
    public static class Utility
    {
        public static (StorageKey key, byte[] value) VerifyProof(UInt256 rootHash, byte[]? proof)
        {
            ArgumentNullException.ThrowIfNull(proof);

            var proofs = new HashSet<byte[]>();

            using MemoryStream stream = new(proof, false);
            using BinaryReader reader = new(stream, Neo.Utility.StrictUTF8);

            var keyBuffer = reader.ReadVarBytes(Node.MaxKeyLength);

            var count = reader.ReadVarInt();
            for (ulong i = 0; i < count; i++)
            {
                proofs.Add(reader.ReadVarBytes());
            } 

            var value = Trie.VerifyProof(rootHash, keyBuffer, proofs);
            if (value is null) throw new Exception("Verification failed");

            // Note, StorageKey.Deserialized was removed in Neo 3.3.0
            //       so VerifyProof has to deserialize StorageKey directly
            var key = new StorageKey()
            {
                Id = BinaryPrimitives.ReadInt32LittleEndian(keyBuffer),
                Key = keyBuffer.AsMemory(4)
            };
            return (key, value);
        }
    }
}
