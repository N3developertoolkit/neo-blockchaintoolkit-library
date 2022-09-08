using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Persistence;
using Neo.IO;
using Neo.Network.RPC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit.Models
{
    public readonly record struct BranchInfo(
        uint Network,
        byte AddressVersion,
        uint Index,
        UInt256 IndexHash,
        UInt256 RootHash,
        IReadOnlyDictionary<int, UInt160> ContractMap)
    {
        public ProtocolSettings ProtocolSettings => ProtocolSettings.Default with
        {
            AddressVersion = AddressVersion,
            Network = Network,
        };

        public static Task<BranchInfo> GetBranchInfoAsync(string url, uint index) => GetBranchInfoAsync(new Uri(url), index);

        public static async Task<BranchInfo> GetBranchInfoAsync(Uri url, uint index)
        {
            using var client = new RpcClient(url);
            return await client.GetBranchInfoAsync(index).ConfigureAwait(false);
        }

        public static BranchInfo Load(JObject json)
        {
            var network = json.Value<uint>("network");
            var addressVersion = json.Value<byte>("address-version");
            var index = json.Value<uint>("index");
            var indexHash = UInt256.Parse(json.Value<string>("index-hash"));
            var rootHash = UInt256.Parse(json.Value<string>("rootHash"));

            var mapBuilder = ImmutableDictionary.CreateBuilder<int, UInt160>();
            var mapJson = json["contract-map"] as JObject;
            if (mapJson is not null)
            {
                foreach (var (key, value) in mapJson)
                {
                    mapBuilder.Add(
                        int.Parse(key),
                        UInt160.Parse(value!.Value<string>()));
                }
            }
            return new BranchInfo(network, addressVersion, index, indexHash, rootHash, mapBuilder.ToImmutable());
        }

        public void WriteJson(JsonWriter writer)
        {
            using var _ = writer.WriteObject();
            writer.WriteProperty("network", Network);
            writer.WriteProperty("address-version", AddressVersion);
            writer.WriteProperty("index", Index);
            writer.WriteProperty("index-hash", $"{IndexHash}");
            writer.WriteProperty("root-hash", $"{RootHash}");
            using var __ = writer.WritePropertyObject("contract-map");
            foreach (var (k, v) in ContractMap)
            {
                writer.WriteProperty($"{k}", $"{v}");
            }
        }
    }
}
