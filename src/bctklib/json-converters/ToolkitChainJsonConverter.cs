using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Neo.BlockchainToolkit.Models;

namespace Neo.BlockchainToolkit.JsonConverters
{
    public class ToolkitChainJsonConverter : JsonConverter<ToolkitChain>
    {
        public override ToolkitChain Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var json = JsonElement.ParseValue(ref reader);
            return ToolkitChain.ReadJson(json);
        }

        public override void Write(Utf8JsonWriter writer, ToolkitChain value, JsonSerializerOptions options)
        {
            value.WriteJson(writer);
        }
    }
}
