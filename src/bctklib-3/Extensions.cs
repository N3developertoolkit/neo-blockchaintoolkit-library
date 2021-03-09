
using System;
using System.IO.Abstractions;
using System.Linq;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.Wallets;
using Newtonsoft.Json;

namespace Neo.BlockchainToolkit
{
    public static class Extensions
    {
        public static ProtocolSettings GetProtocolSettings(this ExpressChain? chain, uint secondsPerBlock = 0)
        {
            return chain == null 
                ? ProtocolSettings.Default 
                : ProtocolSettings.Default with {
                    Magic = chain.Magic,
                    AddressVersion = chain.AddressVersion,
                    MillisecondsPerBlock = secondsPerBlock == 0 ? 15000 : secondsPerBlock * 1000,
                    ValidatorsCount = chain.ConsensusNodes.Count,
                    StandbyCommittee = chain.ConsensusNodes.Select(GetPublicKey).ToArray(),
                    SeedList = chain.ConsensusNodes
                        .Select(n => $"{System.Net.IPAddress.Loopback}:{n.TcpPort}")
                        .ToArray(),
                };

            static ECPoint GetPublicKey(ExpressConsensusNode node)
                => new KeyPair(node.Wallet.Accounts.Select(a => a.PrivateKey).Distinct().Single().HexToBytes()).PublicKey;
        }

        public static ExpressChain LoadChain(this IFileSystem fileSystem, string path)
        {
            var serializer = new JsonSerializer();
            using var stream = fileSystem.File.OpenRead(path);
            using var streamReader = new System.IO.StreamReader(stream);
            using var reader = new JsonTextReader(streamReader);
            return serializer.Deserialize<ExpressChain>(reader)
                ?? throw new Exception($"Cannot load Neo-Express instance information from {path}");
        }

        public static void SaveChain(this IFileSystem fileSystem, ExpressChain chain, string path)
        {
            var serializer = new JsonSerializer();
            using var stream = fileSystem.File.Open(path, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            using var streamWriter = new System.IO.StreamWriter(stream);
            using var writer = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented };
            serializer.Serialize(writer, chain);
        }
    }
}
