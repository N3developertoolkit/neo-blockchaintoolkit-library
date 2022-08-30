using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.SmartContract;
using Neo.Wallets;

namespace Neo.BlockchainToolkit.Models
{
    public class ToolkitWallet : Wallet
    {
        readonly Dictionary<UInt160, ToolkitWalletAccount> accounts = new();

        public override string Name { get; }
        public override Version Version => Version.Parse(ThisAssembly.AssemblyFileVersion);

        public ToolkitWallet(string name, ProtocolSettings settings) : base(string.Empty, settings)
        {
            this.Name = name;
        }

        public ToolkitWallet(string name, IEnumerable<ToolkitWalletAccount> accounts, ProtocolSettings settings)
            : this(name, settings)
        {
            foreach (var account in accounts)
            {
                this.accounts.Add(account.ScriptHash, account);
            }
        }

        public ExpressWallet ToExpress()
        {
            return new ExpressWallet
            {
                Name = Name,
                Accounts = accounts.Values.Select(a => a.ToExpress()).ToList(),
            };
        }

        public static ToolkitWallet FromExpress(ExpressWallet wallet, ProtocolSettings settings)
        {
            var accounts = wallet.Accounts.Select(a => ToolkitWalletAccount.FromExpress(a, settings));
            return new ToolkitWallet(wallet.Name, accounts, settings);
        }

        public override bool Contains(UInt160 scriptHash) => accounts.ContainsKey(scriptHash);

        public override WalletAccount CreateAccount(byte[] privateKey)
        {
            var key = new KeyPair(privateKey);
            var contract = Contract.CreateSignatureContract(key.PublicKey);
            return CreateAccount(contract, key);
        }

        public override WalletAccount CreateAccount(Contract contract, KeyPair? key = null)
        {
            var account = new ToolkitWalletAccount(key, contract, ProtocolSettings);
            accounts.Add(account.ScriptHash, account);
            return account;
        }

        public override WalletAccount CreateAccount(UInt160 scriptHash)
        {
            var account = new ToolkitWalletAccount(scriptHash, ProtocolSettings);
            accounts.Add(account.ScriptHash, account);
            return account;
        }

        public override bool DeleteAccount(UInt160 scriptHash) => accounts.Remove(scriptHash);

        public override WalletAccount? GetAccount(UInt160 scriptHash) => accounts.GetValueOrDefault(scriptHash);

        public override IEnumerable<WalletAccount> GetAccounts() => accounts.Values;

        public override bool VerifyPassword(string password) => true;

        public override bool ChangePassword(string oldPassword, string newPassword)
            => throw new NotSupportedException();

        public override void Delete()
            => throw new NotSupportedException();

        public override void Save()
            => throw new NotSupportedException();
    }
}
