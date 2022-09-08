using System;
using System.Linq;
using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

            public static Account Load(JObject json, ProtocolSettings settings)
            {
                var scriptHash = UInt160.Parse(json.Value<string>("script-hash"));

                KeyPair? keyPair = null;
                if (json.TryGetValue("private-key", out var token)
                    && token.Type != JTokenType.Null)
                {
                    if (token.Type != JTokenType.String) throw new Exception();
                    keyPair = new KeyPair(Convert.FromBase64String(token.Value<string>()));
                }

                Contract? contract = null;
                if (json.TryGetValue("contract", out token)
                    && token.Type != JTokenType.Null)
                {
                    var script = Convert.FromBase64String(token.Value<string>("script"));
                    var @params = Array.Empty<ContractParameterType>();
                    var paramArray = token["parameters"] as JArray;
                    if (paramArray is not null)
                    {
                        @params = paramArray
                            .Select(t => Enum.Parse<ContractParameterType>(t.Value<string>()))
                            .ToArray();
                    }
                    contract = new Contract() { Script = script, ParameterList = @params };
                    scriptHash = contract.ScriptHash;
                }

                var label = json.TryGetValue("label", out token) ? token.Value<string>() : null;
                var @lock = json.TryGetValue("lock", out token) && token.Value<bool>();
                var isDefault = json.TryGetValue("is-default", out token) && token.Value<bool>();

                return new Account(keyPair, scriptHash, settings)
                {
                    Contract = contract,
                    IsDefault = isDefault,
                    Label = label,
                    Lock = @lock,
                };
            }

            public void WriteJson(JsonWriter writer)
            {
                using var _ = writer.WriteObject();
                writer.WriteProperty("script-hash", $"{ScriptHash}");
                var key = GetKey();
                if (key is not null)
                {
                    writer.WritePropertyBase64("private-key", key.PrivateKey);
                }
                if (Contract is not null)
                {
                    using var __ = writer.WritePropertyObject("contract");
                    writer.WritePropertyBase64("script", Contract.Script);
                    using var ___ = writer.WritePropertyArray("parameters");
                    foreach (var p in Contract.ParameterList)
                    {
                        var type = Enum.GetName(p) ?? throw new Exception($"Invalid {nameof(ContractParameterType)}");
                        writer.WriteValue(type);
                    }
                }
                if (Label is not null)
                {
                    writer.WriteProperty("label", Label);
                }
                if (IsDefault)
                {
                    writer.WriteProperty("is-default", IsDefault);
                }
                if (Lock)
                {
                    writer.WriteProperty("lock", Lock);
                }
            }
        }
    }
}
