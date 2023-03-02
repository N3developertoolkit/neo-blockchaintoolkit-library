using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit.Models
{
    public record ExpressChain(
        uint Network,
        byte AddressVersion,
        IReadOnlyList<ToolkitConsensusNode> ConsensusNodes,
        IReadOnlyList<ToolkitWallet> Wallets,
        IReadOnlyDictionary<string, string> Settings)
    {
        readonly static ImmutableHashSet<uint> KNOWN_NETWORK_NUMBERS = ImmutableHashSet.Create<uint>(
            /* Neo 2 MainNet */ 7630401,
            /* Neo 2 TestNet */ 1953787457,
            /* Neo 3 MainNet */ 860833102,
            /* Neo 3 T5 TestNet */ 894710606,
            /* Neo 3 T4 TestNet */ 877933390,
            /* Neo 3 RC3 TestNet */ 844378958,
            /* Neo 3 RC1 TestNet */ 827601742,
            /* Neo 3 Preview5 TestNet */ 894448462);

        public static uint GenerateNetworkValue()
        {
            var random = new Random();
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            while (true)
            {
                random.NextBytes(buffer);
                uint network = BinaryPrimitives.ReadUInt32LittleEndian(buffer);

                if (network > 0 && !KNOWN_NETWORK_NUMBERS.Contains(network))
                {
                    return network;
                }
            }
        }

        public ProtocolSettings ProtocolSettings 
        {
            get
            {
                return ProtocolSettings.Default with 
                {
                    Network = Network,
                    AddressVersion = AddressVersion,
                    ValidatorsCount = ConsensusNodes.Count,
                    StandbyCommittee = ConsensusNodes.Select(GetPublicKey).ToArray(),
                    SeedList = ConsensusNodes
                        .Select(n => $"{BindAddress}:{n.TcpPort}")
                        .ToArray(),
                };

                static ECPoint GetPublicKey(ToolkitConsensusNode node)
                    => node.Wallet.GetDefaultAccount().GetKey().PublicKey;
            }
        } 

        public Contract ConsensusContract 
        {
            get
            {
                var keys = ConsensusNodes
                    .Select(n => n.Wallet.GetDefaultAccount().GetKey().PublicKey)
                    .ToArray();
                return Contract.CreateMultiSigContract((keys.Length * 2 / 3) + 1, keys);
            }
        }

        public uint MillisecondsPerBlock => Settings.TryGetValue("chain.SecondsPerBlock", out var value)
            && uint.TryParse(value, out var result)
                ? result * 1000 : ProtocolSettings.Default.MillisecondsPerBlock ;

        public IPAddress BindAddress => Settings.TryGetValue("rpc.BindAddress", out var value) 
            && IPAddress.TryParse(value, out var result)
                ? result : IPAddress.Loopback; 

        public long MaxGasInvoke => Settings.TryGetValue("rpc.MaxGasInvoke", out var value) 
            && long.TryParse(value, out var result)
                ? result : (long)new BigDecimal(10M, NativeContract.GAS.Decimals).Value; 

        public long MaxFee => Settings.TryGetValue("rpc.MaxFee", out var value) 
            && long.TryParse(value, out var result)
                ? result : (long)new BigDecimal(0.1M, NativeContract.GAS.Decimals).Value; 

        public long MaxIteratorResultItems => Settings.TryGetValue("rpc.MaxIteratorResultItems", out var value) 
            && int.TryParse(value, out var result)
                ? result : 100; 

        public bool SessionEnabled => Settings.TryGetValue("rpc.SessionEnabled", out var value) 
            && bool.TryParse(value, out var result)
                ? result : true;

        public static ExpressChain Parse(JObject json)
        {
            var network = json.Value<uint>("magic");
            var addressVersion = json.Value<byte>("address-version");
            var protocolSettings = ProtocolSettings.Default with 
            {
                Network = network,
                AddressVersion = addressVersion
            };

            var nodes = (json["consensus-nodes"] ?? throw new JsonException())
                .Cast<JObject>()
                .Select(n => ToolkitConsensusNode.Parse(n, protocolSettings))
                .ToList();

            var wallets = (json["wallets"] ?? Enumerable.Empty<JToken>())
                .Cast<JObject>()
                .Select(w => ToolkitWallet.Parse(w, protocolSettings))
                .ToList();

            var settings = (json["settings"] ?? Enumerable.Empty<JToken>())
                .Cast<JProperty>()
                .ToDictionary(p => p.Name, p => $"{p.Value}");

            return new ExpressChain(network, addressVersion, nodes, wallets, settings);
        }
    }
}
