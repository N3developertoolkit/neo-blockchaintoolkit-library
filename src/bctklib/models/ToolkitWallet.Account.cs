using System;
using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json;

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
