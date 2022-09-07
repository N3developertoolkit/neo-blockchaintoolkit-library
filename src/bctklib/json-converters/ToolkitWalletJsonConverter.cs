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
            return ReadJson(json, settings);
        }

        public static ToolkitWallet ReadJson(JsonElement json, ProtocolSettings settings)
        {
            var name = json.GetProperty("name").GetString() ?? throw new JsonException("name");
            var accounts = json.TryGetProperty("accounts", out var prop)
                ? prop.EnumerateArray().Select(a => ReadWalletAccountJson(a, settings))
                : Enumerable.Empty<ToolkitWallet.Account>();
            return new ToolkitWallet(name, accounts, settings);

            static ToolkitWallet.Account ReadWalletAccountJson(JsonElement json, ProtocolSettings settings)
            {
                KeyPair? keyPair = null;
                JsonElement prop;
                if (json.TryGetProperty("private-key", out prop)
                    && prop.ValueKind != JsonValueKind.Null)
                {
                    var privateKey = prop.TryGetBytesFromBase64(out var _key)
                        ? _key
                        : Convert.FromHexString(prop.GetString() ?? throw new JsonException("private-key"));
                    keyPair = new KeyPair(privateKey);
                }
                var label = json.TryGetProperty("label", out prop) ? prop.GetString() : null;
                var @lock = json.TryGetProperty("lock", out prop) ? prop.GetBoolean() : false;
                var isDefault = json.TryGetProperty("is-default", out prop) ? prop.GetBoolean() : false;

                var contract = GetContract(json);
                var scriptHash = contract is not null ? contract.ScriptHash : GetScriptHash(json, settings);
                return new ToolkitWallet.Account(keyPair, scriptHash, settings)
                {
                    Contract = contract,
                    IsDefault = isDefault,
                    Label = label,
                    Lock = @lock
                };

                static UInt160 GetScriptHash(JsonElement json, ProtocolSettings settings)
                {
                    var address = json.GetProperty("script-hash").GetString() ?? throw new JsonException("script-hash");
                    return address.ToScriptHash(settings.AddressVersion);
                }

                static Contract? GetContract(JsonElement json)
                {
                    if (!json.TryGetProperty("contract", out var contract)) return null;
                    if (contract.ValueKind == JsonValueKind.Null) return null;
                    var scriptProp = contract.GetProperty("script");
                    var script = scriptProp.TryGetBytesFromBase64(out var _script)
                        ? _script
                        : Convert.FromHexString(scriptProp.GetString() ?? throw new JsonException("script"));
                    var parameters = contract.GetProperty("parameters")
                        .EnumerateArray()
                        .Select(v => Enum.Parse<ContractParameterType>(v.GetString() ?? throw new JsonException("parameters")));
                    return new Contract()
                    {
                        Script = script,
                        ParameterList = parameters.ToArray()
                    };
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, ToolkitWallet value, JsonSerializerOptions options)
        {
            WriteJson(writer, value);
        }

        public static void WriteJson(Utf8JsonWriter writer, ToolkitWallet value)
        {
            writer.WriteStartObject();
            writer.WriteString("name", value.Name);
            writer.WriteStartArray("accounts");
            foreach (var account in value.GetAccounts())
            {
                WriteWalletAccountJson(writer, account);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();


            static void WriteWalletAccountJson(Utf8JsonWriter writer, WalletAccount value)
            {
                writer.WriteStartObject();
                var key = value.GetKey();
                if (key is not null)
                {
                    writer.WriteBase64String("private-key", key.PrivateKey);
                }
                if (value.Contract is not null)
                {
                    writer.WriteStartObject("contract");
                    writer.WriteBase64String("script", value.Contract.Script);
                    writer.WriteStartArray("parameters");
                    foreach (var p in value.Contract.ParameterList)
                    {
                        var type = Enum.GetName<ContractParameterType>(p)
                            ?? throw new Exception("Invalid ContractParameterType");
                        writer.WriteStringValue(type);
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteString("script-hash", value.Address);
                }
                if (value.Label is not null)
                {
                    writer.WriteString("label", value.Label);
                }
                if (value.IsDefault)
                {
                    writer.WriteBoolean("is-default", value.IsDefault);
                }
                if (value.Lock)
                {
                    writer.WriteBoolean("lock", value.Lock);
                }
                writer.WriteEndObject();
            }
        }
    }
}
