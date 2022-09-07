using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.Wallets;

namespace Neo.BlockchainToolkit.JsonConverters
{
    public class ToolkitWalletJsonConverter : JsonConverter<ToolkitWallet>
    {
        readonly ProtocolSettings settings;

        public ToolkitWalletJsonConverter(ProtocolSettings settings)
        {
            this.settings = settings;
        }

        public override ToolkitWallet? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var json = JsonElement.ParseValue(ref reader);
            return ToolkitWallet.ReadJson(json, settings);
        }

        public override void Write(Utf8JsonWriter writer, ToolkitWallet value, JsonSerializerOptions options)
        {
            value.WriteJson(writer);
        }
    }
}
