using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Neo.BlockchainToolkit.Models
{
    public class ExpressChain
    {
        private readonly static ImmutableHashSet<uint> KNOWN_NETWORK_NUMBERS = ImmutableHashSet.Create<uint>(
            /* Neo 2 MainNet */ 7630401,
            /* Neo 2 TestNet */ 1953787457,
            /* Neo 3 MainNet */ 5195086,
            /* Neo 3 TestNet */ 1951352142,
            /* Neo 3 RC1 TestNet */ 827601742,
            /* Neo 3 RC2 TestNet */ 844378958);

        public static uint GenerateNetworkValue()
        {
            var random = new Random();
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            while (true)
            {
                random.NextBytes(buffer);
                uint network = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(buffer);

                if (network > 0 && !KNOWN_NETWORK_NUMBERS.Contains(network))
                {
                    return network;
                }
            }
        }

        [JsonProperty("magic")]
        public uint Network { get; set; }

        [JsonProperty("address-version")]
        public byte AddressVersion { get; set; } = ProtocolSettings.Default.AddressVersion;

        [JsonProperty("consensus-nodes")]
        public List<ExpressConsensusNode> ConsensusNodes { get; set; } = new List<ExpressConsensusNode>();

        [JsonProperty("wallets")]
        public List<ExpressWallet> Wallets { get; set; } = new List<ExpressWallet>();

        [JsonProperty("settings")]
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
    }
}
