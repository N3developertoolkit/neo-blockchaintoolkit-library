
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;

namespace Neo.BlockchainToolkit
{
    public static partial class ContractInvocationParser
    {
        public delegate bool TryGetUInt160(string value, out UInt160 account);

        public interface IArgBinders
        {
            byte AddressVersion { get; }
            IFileSystem FileSystem { get; }
            TryGetUInt160? TryGetAccount { get; }
            TryGetUInt160? TryGetContract { get; }
        }

        class NullArgBinders : IArgBinders
        {
            public byte AddressVersion => ProtocolSettings.Default.AddressVersion;
            public IFileSystem FileSystem => ContractInvocationParser.defaultFileSystem.Value;
            public TryGetUInt160? TryGetAccount => null;
            public TryGetUInt160? TryGetContract => null;
        }

        class ExpressArgBinders : IArgBinders
        {
            ExpressChain chain;
            IFileSystem? fileSystem;
            IReadOnlyDictionary<string, UInt160> contractNameMap;

            public ExpressArgBinders(ExpressChain chain, IEnumerable<(UInt160 hash, ContractManifest manifest)> contracts, IFileSystem? fileSystem = null)
                : this(chain, contracts.ToDictionary(t => t.manifest.Name, t => t.hash), fileSystem)
            {
            }

            public ExpressArgBinders(ExpressChain chain, IReadOnlyDictionary<string, UInt160> contracts, IFileSystem? fileSystem = null)
            {
                this.chain = chain;
                this.fileSystem = fileSystem;
                this.contractNameMap = contracts;
            }

            public byte AddressVersion => chain?.AddressVersion ?? ProtocolSettings.Default.AddressVersion;

            public IFileSystem FileSystem => fileSystem ?? ContractInvocationParser.defaultFileSystem.Value;

            public TryGetUInt160 TryGetAccount => (string name, out UInt160 scriptHash) =>
                {
                    if (chain.TryGetDefaultAccount(name.AsSpan(), out var account) == true)
                    {
                        scriptHash = account.ToScriptHash(chain.AddressVersion);
                        return true;
                    }

                    scriptHash = UInt160.Zero;
                    return false;
                };

            public TryGetUInt160 TryGetContract => (string name, out UInt160 scriptHash) =>
                {
                    if (contractNameMap.TryGetValue(name, out var hash))
                    {
                        scriptHash = hash;
                        return true;
                    }

                    foreach (var kvp in contractNameMap)
                    {
                        if (string.Equals(name, kvp.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            scriptHash = kvp.Value;
                            return true;
                        }
                    }

                    foreach (var contract in NativeContract.Contracts)
                    {
                        if (string.Equals(name, contract.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            scriptHash = contract.Hash;
                            return true;
                        }
                    }

                    scriptHash = UInt160.Zero;
                    return false;
                };
        }
    }
}
