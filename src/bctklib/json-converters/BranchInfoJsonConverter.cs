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
            return BranchInfo.ReadJson(json);
        }

        public override void Write(Utf8JsonWriter writer, BranchInfo value, JsonSerializerOptions options)
        {
            value.WriteJson(writer);
        }
    }
}