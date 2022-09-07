using System;
using System.Linq;
using System.Text.Json;
using Neo.SmartContract;
using Neo.Wallets;

namespace Neo.BlockchainToolkit.Models
{
    public partial class ToolkitWallet
    {
        internal class Account : WalletAccount
        {
            readonly KeyPair? key;

            public Account(KeyPair? key, UInt160 scriptHash, ProtocolSettings settings)
                : base(scriptHash, settings)
            {
                this.key = key;
            }

            public override bool HasKey => key is not null;

            public override KeyPair? GetKey() => key;

            public static ToolkitWallet.Account ReadJson(JsonElement json, ProtocolSettings settings)
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

            public void WriteJson(Utf8JsonWriter writer)
            {
                writer.WriteStartObject();
                var key = GetKey();
                if (key is not null)
                {
                    writer.WriteBase64String("private-key", key.PrivateKey);
                }
                if (Contract is not null)
                {
                    writer.WriteStartObject("contract");
                    writer.WriteBase64String("script", Contract.Script);
                    writer.WriteStartArray("parameters");
                    foreach (var p in Contract.ParameterList)
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
                    writer.WriteString("script-hash", Address);
                }
                if (Label is not null)
                {
                    writer.WriteString("label", Label);
                }
                if (IsDefault)
                {
                    writer.WriteBoolean("is-default", IsDefault);
                }
                if (Lock)
                {
                    writer.WriteBoolean("lock", Lock);
                }
                writer.WriteEndObject();
            }
        }
    }
}
