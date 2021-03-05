using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Neo.BlockchainToolkit.Models
{
    public class ExpressChain
    {
        private readonly static ImmutableArray<uint> KNOWN_MAGIC_NUMBERS = ImmutableArray.Create<uint>(
            /* Neo 2 MainNet */ 7630401,
            /* Neo 2 TestNet */ 1953787457,
            /* Neo 3 MainNet */ 5195086,
            /* Neo 3 TestNet */ 1951352142);

        public static uint GenerateMagicValue()
        {
            var random = new Random();
            while (true)
            {
                uint magic = (uint)random.Next(int.MaxValue);

                if (!KNOWN_MAGIC_NUMBERS.Contains(magic))
                {
                    return magic;
                }
            }
        }

        [JsonProperty("magic")]
        public uint Magic { get; set; }

        [JsonProperty("consensus-nodes")]
        public List<ExpressConsensusNode> ConsensusNodes { get; set; } = new List<ExpressConsensusNode>();

        [JsonProperty("wallets")]
        public List<ExpressWallet> Wallets { get; set; } = new List<ExpressWallet>();

        [JsonProperty("contracts")]
        public List<ExpressContract> Contracts { get; set; } = new List<ExpressContract>();
    }
}
