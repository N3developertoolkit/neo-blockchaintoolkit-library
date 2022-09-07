using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.IO;
using Neo.Wallets;

namespace Neo.BlockchainToolkit.JsonConverters
{
    public class BranchInfoJsonConverter : JsonConverter<BranchInfo>
    {
        public override BranchInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var json = JsonElement.ParseValue(ref reader);
            return ReadJson(json);
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

        public override void Write(Utf8JsonWriter writer, BranchInfo value, JsonSerializerOptions options)
        {
            WriteJson(writer, value);
        }

        public static void WriteJson(Utf8JsonWriter writer, BranchInfo value)
        {
            writer.WriteStartObject();
            writer.WriteNumber("network", value.network);
            writer.WriteNumber("address-version", value.addressVersion);
            writer.WriteNumber("index", value.index);
            writer.WriteBase64String("index-hash", value.indexHash.ToArray());
            writer.WriteBase64String("root-hash", value.rootHash.ToArray());
            writer.WriteStartObject("contract-map");
            foreach (var (k, v) in value.contractMap)
            {
                writer.WritePropertyName($"{k}");
                writer.WriteBase64StringValue(v.ToArray());
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }
}