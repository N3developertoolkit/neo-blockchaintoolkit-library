
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using Neo.Cryptography.MPTTrie;
using Neo.IO;
using Neo.SmartContract;
using static Neo.BlockchainToolkit.Constants;

namespace Neo.BlockchainToolkit
{
    public static class Utility
    {
        public static void EnableAnsiEscapeSequences()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                const int STD_OUTPUT_HANDLE = -11;
                var stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);

                if (GetConsoleMode(stdOutHandle, out uint outMode))
                {
                    const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
                    const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

                    outMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
                    SetConsoleMode(stdOutHandle, outMode);
                }
            }

            [DllImport("kernel32.dll")]
            static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

            [DllImport("kernel32.dll")]
            static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern IntPtr GetStdHandle(int nStdHandle);
        }

        public static bool TryParseRpcUri(string value, [NotNullWhen(true)] out Uri? uri)
        {
            if (value.Equals("mainnet", StringComparison.OrdinalIgnoreCase))
            {
                uri = new Uri(MAINNET_RPC_ENDPOINTS[0]);
                return true;
            }

            if (value.Equals("testnet", StringComparison.OrdinalIgnoreCase))
            {
                uri = new Uri(TESTNET_RPC_ENDPOINTS[0]);
                return true;
            }

            return Uri.TryCreate(value, UriKind.Absolute, out uri)
                && uri is not null
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

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
            //       https://github.com/neo-project/neo/issues/2765
            var key = new StorageKey()
            {
                Id = BinaryPrimitives.ReadInt32LittleEndian(keyBuffer),
                Key = keyBuffer.AsMemory(4)
            };
            return (key, value);
        }
    }
}
