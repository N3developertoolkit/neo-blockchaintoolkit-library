using System;
using System.Linq;
using Neo.SmartContract;
using Neo.Wallets;

namespace Neo.BlockchainToolkit.Models
{
    class ToolkitWalletAccount : WalletAccount
    {
        readonly KeyPair? key;

        public override bool HasKey => key is not null;

        public ToolkitWalletAccount(KeyPair? key, Contract contract, ProtocolSettings settings)
            : base(contract.ScriptHash, settings)
        {
            this.key = key;
            this.Contract = contract;
        }

        public ToolkitWalletAccount(UInt160 scriptHash, ProtocolSettings settings) : this(null, scriptHash, settings)
        {
        }

        public ToolkitWalletAccount(KeyPair? key, UInt160 scriptHash, ProtocolSettings settings) : base(scriptHash, settings)
        {
        }

        public ExpressWalletAccount ToExpress()
        {
            return new ExpressWalletAccount
            {
                PrivateKey = key is null
                    ? string.Empty
                    : Convert.ToHexString(key.PrivateKey),
                ScriptHash = ScriptHash.ToAddress(ProtocolSettings.AddressVersion),
                Label = Label,
                IsDefault = IsDefault,
                Contract = Contract is null
                    ? null
                    : new ExpressWalletAccount.AccountContract
                    {
                        Script = Convert.ToHexString(Contract.Script),
                        Parameters = Contract.ParameterList
                            .Select(p => Enum.GetName<ContractParameterType>(p) ?? throw new Exception())
                            .ToList()
                    }
            };
        }

        public static ToolkitWalletAccount FromExpress(ExpressWalletAccount account, ProtocolSettings settings)
        {
            var key = string.IsNullOrEmpty(account.PrivateKey) 
                ? null 
                : new KeyPair(Convert.FromHexString(account.PrivateKey));
            var contract = account.Contract is null 
                ? null
                : new Contract
                {
                    Script = Convert.FromHexString(account.Contract.Script),
                    ParameterList = account.Contract.Parameters
                        .Select(Enum.Parse<ContractParameterType>)
                        .ToArray()
                };
            var scriptHash = account.ScriptHash.ToScriptHash(settings.AddressVersion);
            if (contract is null) 
            {
                return new ToolkitWalletAccount(key, scriptHash, settings);
            }
            else 
            {
                if (contract.ScriptHash != scriptHash) 
                {
                    throw new Exception($"Invalid {nameof(ExpressWalletAccount)}");
                }
                return new ToolkitWalletAccount(key, contract, settings);
            }
        }

        public override KeyPair? GetKey() => key;
    }
}
