using System;
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

            public static Account Parse(JObject json, ProtocolSettings settings)
            {
                var isDefault = json.TryGetValue("is-default", out var token) && token.Value<bool>();
                var label = json.TryGetValue("label", out token) ? token.Value<string>() : null;

                if (json.TryGetValue("private-key", out token) && token.Type == JTokenType.String)
                {
                    var key = token.Value<string>();
                    if (!string.IsNullOrEmpty(key))
                    {
                        var keyPair = new KeyPair(Convert.FromHexString(key));
                        var contract = Contract.CreateSignatureContract(keyPair.PublicKey);
                        return new Account(keyPair, contract.ScriptHash, settings)
                        {
                            Contract = contract,
                            IsDefault = isDefault,
                            Label = label
                        };
                    }
                }

                throw new NotSupportedException("Toolkit Wallet Accounts must have a private key");
            }

            public void WriteJson(JsonWriter writer)
            {
                var key = GetKey();
                if (key is null) throw new Exception("Invalid ToolkitWallet Address");

                writer.WriteStartObject();
                writer.WriteProperty("private-key", Convert.ToHexString(key.PrivateKey));
                writer.WriteProperty("address", Address);
                writer.WriteProperty("script-hash", ScriptHash.ToString());
                if (Label is not null) writer.WriteProperty("label", Label);
                writer.WriteProperty("is-default", IsDefault);
                if (Lock) writer.WriteProperty("lock", Lock);

                if (Contract is not null)
                {
                    writer.WritePropertyName("contract");
                    writer.WriteStartObject();
                    writer.WriteProperty("script", Convert.ToHexString(Contract.Script));
                    writer.WritePropertyName("parameters");
                    writer.WriteStartArray();
                    foreach (var p in Contract.ParameterList)
                    {
                        var type = Enum.GetName(p) ?? throw new Exception($"Invalid {nameof(ContractParameterType)}");
                        writer.WriteValue(type);
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
        }
    }
}
