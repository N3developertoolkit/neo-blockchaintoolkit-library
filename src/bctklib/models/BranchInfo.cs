using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Persistence;
using Neo.IO;
using Neo.Network.RPC;

namespace Neo.BlockchainToolkit.Models
{
    public readonly record struct BranchInfo(
        uint network,
        byte addressVersion,
        uint index,
        UInt256 indexHash,
        UInt256 rootHash,
        IReadOnlyDictionary<int, UInt160> contractMap)
    {
        public Neo.ProtocolSettings ProtocolSettings => Neo.ProtocolSettings.Default with
        {
            AddressVersion = addressVersion,
            Network = network,
        };

        public static Task<BranchInfo> GetBranchInfoAsync(string url, uint index) => GetBranchInfoAsync(new Uri(url), index);

        public static async Task<BranchInfo> GetBranchInfoAsync(Uri url, uint index)
        {
            using var client = new RpcClient(url);
            return await client.GetBranchInfoAsync(index).ConfigureAwait(false);
        }

        public static BranchInfo ReadJson(JsonElement json)
        {
            var network = json.GetProperty("network").GetUInt32();
            var addressVersion = json.GetProperty("address-version").GetByte();
            var index = json.GetProperty("index").GetUInt32();
            var indexHash = new UInt256(json.GetProperty("index-hash").GetBytesFromBase64());
            var rootHash = new UInt256(json.GetProperty("root-hash").GetBytesFromBase64());
            var contractMapBuilder = ImmutableDictionary.CreateBuilder<int, UInt160>();
            foreach (var prop in json.GetProperty("contract-map").EnumerateObject())
            {
                contractMapBuilder.Add(int.Parse(prop.Name), new UInt160(prop.Value.GetBytesFromBase64()));
            }

            return new BranchInfo(network, addressVersion, index, indexHash, rootHash, contractMapBuilder.ToImmutable());
        }

        public void WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteNumber("network", network);
            writer.WriteNumber("address-version", addressVersion);
            writer.WriteNumber("index", index);
            writer.WriteBase64String("index-hash", indexHash.ToArray());
            writer.WriteBase64String("root-hash", rootHash.ToArray());
            writer.WriteStartObject("contract-map");
            foreach (var (k, v) in contractMap)
            {
                writer.WritePropertyName($"{k}");
                writer.WriteBase64StringValue(v.ToArray());
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }
}
