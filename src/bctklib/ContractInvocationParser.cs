
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using System.Numerics;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.SmartContract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneOf;
using None = OneOf.Types.None;

namespace Neo.BlockchainToolkit
{
    // Note, string is not a valid arg type. Strings need to be converted into byte arrays 
    // by later processing steps. However, the specific conversion of string -> byte array
    // is string content dependent

    using PrimitiveArg = OneOf<bool, BigInteger, ImmutableArray<byte>, string>;
    using BindingFunc = Func<string, Action<string>, OneOf<PrimitiveContractArg, OneOf.Types.None>>;

    public static partial class ContractInvocationParser
    {
        public delegate bool TryConvert<T>(string value, [MaybeNullWhen(false)] out T account);

        public static IEnumerable<ContractInvocation> ParseInvocations(JToken doc)
        {
            if (doc is JObject obj)
            {
                yield return ParseInvocation(obj);
            }
            else if (doc is JArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is JObject itemObj)
                    {
                        yield return ParseInvocation(itemObj);
                    }
                    else
                    {
                        throw new JsonException("Invalid invocation JSON");
                    }
                }
            }
            else
            {
                throw new JsonException("Invalid invocation JSON");
            }
        }

        public static ContractInvocation ParseInvocation(JObject json)
        {
            var contract = json.Value<string>("contract");
            if (string.IsNullOrEmpty(contract)) throw new JsonException("missing invocation contract property");
            var operation = json.Value<string>("operation");
            if (string.IsNullOrEmpty(operation)) throw new JsonException("missing invocation operation property");

            var callFlags = json.ContainsKey("callFlags")
                ? Enum.Parse<CallFlags>(json.Value<string>("callFlags"))
                // default to CallFlags.All if not specified in JSON
                : CallFlags.All;

            var args = json.TryGetValue("args", out var jsonArgs)
                    ? ParseArgs(jsonArgs)
                    : Array.Empty<ContractArg>();

            return new ContractInvocation(contract, operation, callFlags, args);
        }

        public static IReadOnlyList<ContractArg> ParseArgs(JToken jsonArgs)
        {
            if (jsonArgs is JArray argsArray)
            {
                var array = new ContractArg[argsArray.Count];
                for (int i = 0; i < argsArray.Count; i++)
                {
                    array[i] = ParseArg(argsArray[i]);
                }
                return array;
            }
            else
            {
                var array = new ContractArg[1];
                array[0] = ParseArg(jsonArgs);
                return array;
            }
        }

        internal static ContractArg ParseArg(JToken json)
        {
            if (json is null) return NullContractArg.Null;
            return json.Type switch
            {
                JTokenType.Null => NullContractArg.Null,
                JTokenType.Boolean => new PrimitiveContractArg(json.Value<bool>()),
                JTokenType.Integer => new PrimitiveContractArg(BigInteger.Parse(json.Value<string>())),
                JTokenType.String => new PrimitiveContractArg(json.Value<string>()),
                JTokenType.Array => new ArrayContractArg(json.Select(ParseArg).ToArray()),
                JTokenType.Object => ParseObject((JObject)json),
                _ => throw new ArgumentException($"Invalid JTokenType {json.Type}", nameof(json))
            };
        }

        internal static ContractArg ParseObject(JObject json)
        {
            var typeStr = json.Value<string>("type");
            if (string.IsNullOrEmpty(typeStr)) throw new JsonException("missing type field");
            var type = Enum.Parse<ContractParameterType>(typeStr);
            var value = json["value"];
            if (value is null) return NullContractArg.Null;

            return type switch
            {
                ContractParameterType.Any =>
                    NullContractArg.Null,
                ContractParameterType.Signature or
                ContractParameterType.ByteArray =>
                    ParseBinary(value.Value<string>()),
                ContractParameterType.Boolean =>
                    new PrimitiveContractArg(value.Value<bool>()),
                ContractParameterType.Integer =>
                    new PrimitiveContractArg(BigInteger.Parse(value.Value<string>())),
                ContractParameterType.Hash160 =>
                    PrimitiveContractArg.FromSerializable(UInt160.Parse(value.Value<string>())),
                ContractParameterType.Hash256 =>
                    PrimitiveContractArg.FromSerializable(UInt256.Parse(value.Value<string>())),
                ContractParameterType.PublicKey =>
                    PrimitiveContractArg.FromSerializable(ECPoint.Parse(value.Value<string>(), ECCurve.Secp256r1)),
                ContractParameterType.String =>
                    PrimitiveContractArg.FromString(value.Value<string>()),
                ContractParameterType.Array =>
                    new ArrayContractArg(value.Select(ParseArg).ToArray()),
                ContractParameterType.Map =>
                    new MapContractArg(value.Select(ParseMapElement).ToArray()),
                _ => throw new ArgumentException($"invalid type {type}", nameof(json)),
            };

            static PrimitiveContractArg ParseBinary(string value)
            {
                if (PrimitiveContractArg.TryFromBase64String(value.AsMemory(), out var arg))
                {
                    return arg;
                }

                // TODO: Should I support hex encoding here? 
                //       Core ContractParam.FromJson only supports base64

                throw new JsonException("Invalid binary string");
            }

            static (ContractArg, ContractArg) ParseMapElement(JToken json)
            {
                var keyToken = json["key"] ?? throw new JsonException("Missing key property");
                var key = ParseArg(keyToken);
                if (key is not PrimitiveContractArg) throw new Exception("Map key must be a primitive type");
                var valueToken = json["value"] ?? throw new JsonException("Missing value property");
                var value = ParseArg(valueToken);
                return (key, value);
            }
        }

        public static bool Validate(IEnumerable<ContractInvocation> invocations, ICollection<Diagnostic> diagnostics)
        {
            var visitor = new ValidationVisitor(diagnostics);
            bool valid = true;
            foreach (var invocation in invocations)
            {
                valid &= Validate(invocation, visitor, diagnostics);
            }
            return valid;
        }

        public static bool Validate(ContractInvocation invocation, ICollection<Diagnostic> diagnostics)
        {
            var visitor = new ValidationVisitor(diagnostics);
            return Validate(invocation, visitor, diagnostics);
        }

        static bool Validate(ContractInvocation invocation, ValidationVisitor visitor, ICollection<Diagnostic> diagnostics)
        {
            bool valid = true;
            if (invocation.Contract.IsT1)
            {
                diagnostics.Add(Diagnostic.Error($"Unbound contract hash {invocation.Contract.AsT1}"));
                valid = false;
            }

            if (string.IsNullOrEmpty(invocation.Operation))
            {
                diagnostics.Add(Diagnostic.Error("Invalid operation"));
                valid = false;
            }

            for (int i = 0; i < invocation.Args.Count; i++)
            {
                valid &= visitor.Visit(invocation.Args[i]);
            }
            return valid;
        }

        public interface IBindingParameters
        {
            byte AddressVersion { get; }
            TryConvert<UInt160>? TryGetAccount { get; }
            TryConvert<UInt160>? TryGetContract { get; }
        }

        class DefaultBindingParameters : IBindingParameters
        {
            Models.ExpressChain? chain;

            IReadOnlyDictionary<string, UInt160> contractNameMap;

            public DefaultBindingParameters(ExpressChain? chain, IReadOnlyDictionary<string, UInt160> contracts)
            {
                this.chain = chain;
                this.contractNameMap = contracts;
            }

            public byte AddressVersion => chain?.AddressVersion ?? ProtocolSettings.Default.AddressVersion;

            public TryConvert<UInt160> TryGetAccount => (string name, [MaybeNullWhen(false)] out UInt160 scriptHash) =>
                {
                    if (chain is not null
                        && chain.TryGetDefaultAccount(name, out var account))
                    {
                        scriptHash = Neo.Wallets.Helper.ToScriptHash(account.ScriptHash, AddressVersion);
                        return true;
                    }

                    scriptHash = null!;
                    return false;
                };

            public TryConvert<UInt160> TryGetContract => (string name, [MaybeNullWhen(false)] out UInt160 scriptHash) =>
                {
                    if (contractNameMap.TryGetValue(name, out scriptHash))
                    {
                        return true;
                    }

                    foreach (var kvp in contractNameMap)
                    {
                        if (name.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            scriptHash = kvp.Value;
                            return true;
                        }
                    }

                    return false;
                };
        }

        public static IBindingParameters CreateBindingParameters(Models.ExpressChain? chain, Neo.Persistence.DataCache? snapshot)
        {
            IReadOnlyDictionary<string, UInt160> contractNameMap = snapshot is null
                ? System.Collections.Immutable.ImmutableDictionary<string, UInt160>.Empty
                : Neo.SmartContract.Native.NativeContract.ContractManagement.ListContracts(snapshot)
                    .Where(cs => cs.Id >= 0)
                    .ToDictionary(cs => cs.Manifest.Name, cs => cs.Hash);

            return new DefaultBindingParameters(chain, contractNameMap);
        }

        static Lazy<IFileSystem> defaultFileSystem = new Lazy<IFileSystem>(() => new FileSystem());

        public static IEnumerable<ContractInvocation> Bind(
            IEnumerable<ContractInvocation> invocations,
            IBindingParameters bindingParams,
            Action<string>? reportError = null,
            IFileSystem? fileSystem = null)
        {
            return Bind(invocations, bindingParams.AddressVersion, bindingParams.TryGetAccount, bindingParams.TryGetContract, reportError, fileSystem);
        }

        // Not sure this needs to broken out for test purposes, but leaving it separate from previous Bind method for now
        internal static IEnumerable<ContractInvocation> Bind(
            IEnumerable<ContractInvocation> invocations,
            byte addressVersion,
            TryConvert<UInt160>? tryGetAccount,
            TryConvert<UInt160>? tryGetContract,
            Action<string>? reportError = null,
            IFileSystem? fileSystem = null)
        {
            reportError ??= _ => { };
            fileSystem ??= defaultFileSystem.Value;

            var contractBinder = CreateContractBinder(tryGetContract, reportError);
            var addressBinder = CreateAddressBinder(tryGetAccount, addressVersion, reportError);
            var fileUriBinder = CreateFileUriBinder(fileSystem, reportError);
            var stringBinder = CreateStringBinder(reportError);

            return invocations
                .Select(i => BindContract(i, tryGetContract, reportError))
                .Select(i => BindArguments(i, contractBinder))
                .Select(i => BindArguments(i, addressBinder))
                .Select(i => BindArguments(i, fileUriBinder))
                .Select(i => BindArguments(i, stringBinder));
        }

        public static ContractInvocation BindContract(ContractInvocation invocation, IBindingParameters bindingParams, Action<string>? reportError = null)
        {
            reportError ??= _ => { };
            return BindContract(invocation, bindingParams.TryGetContract, reportError);
        }

        internal static ContractInvocation BindContract(ContractInvocation invocation, TryConvert<UInt160>? tryGetContractHash, Action<string> reportError)
        {
            if (invocation.Contract.TryPickT1(out var contract, out _))
            {
                contract = contract.Length > 0 && contract[0] == '#'
                    ? contract.Substring(1) : contract;
                if (TryConvertContractHash(contract, tryGetContractHash, out var hash))
                {
                    return invocation with { Contract = hash };
                }
                else
                {
                    reportError($"Failed to bind contract {contract}");
                }
            }
            return invocation;
        }

        public static ContractInvocation BindContractArgs(ContractInvocation invocation, IBindingParameters bindingParams, Action<string>? reportError = null)
        {
            reportError ??= _ => { };
            return BindContractArgs(invocation, bindingParams.TryGetContract, reportError);
        }

        internal static ContractInvocation BindContractArgs(ContractInvocation invocation, TryConvert<UInt160>? tryGetContractHash, Action<string> reportError)
        {
            var binder = CreateContractBinder(tryGetContractHash, reportError);
            return BindArguments(invocation, binder);
        }

        internal static BindStringArgVisitor CreateContractBinder(TryConvert<UInt160>? tryGetContractHash, Action<string> reportError)
        {
            BindingFunc func = (value, reportError) =>
            {
                if (value.StartsWith("#"))
                {
                    var hashStr = value.Substring(1);

                    if (TryConvertContractHash(hashStr, tryGetContractHash, out var uint160))
                    {
                        return PrimitiveContractArg.FromSerializable(uint160);
                    }

                    if (UInt256.TryParse(hashStr, out var uint256))
                    {
                        return PrimitiveContractArg.FromSerializable(uint256);
                    }

                    reportError($"failed to bind contract {value}");
                }
                return default(None);
            };
            return new BindStringArgVisitor(func, reportError);
        }

        public static ContractInvocation BindAddressArgs(ContractInvocation invocation, IBindingParameters bindingParams, Action<string>? reportError = null)
        {
            reportError ??= _ => { };
            return BindAddressArgs(invocation, bindingParams.TryGetAccount, bindingParams.AddressVersion, reportError);
        }

        internal static ContractInvocation BindAddressArgs(ContractInvocation invocation, TryConvert<UInt160>? tryGetAddress, byte addressVersion, Action<string> reportError)
        {
            var binder = CreateAddressBinder(tryGetAddress, addressVersion, reportError);
            return BindArguments(invocation, binder);
        }

        internal static BindStringArgVisitor CreateAddressBinder(TryConvert<UInt160>? tryGetAddress, byte addressVersion, Action<string> reportError)
        {
            BindingFunc func = (value, reportError) =>
            {
                if (value.StartsWith("@"))
                {
                    var address = value.Substring(1);

                    if (tryGetAddress is not null
                        && tryGetAddress(address, out var hash))
                    {
                        return PrimitiveContractArg.FromSerializable(hash);
                    }

                    try
                    {
                        hash = Neo.Wallets.Helper.ToScriptHash(address, addressVersion);
                        return PrimitiveContractArg.FromSerializable(hash);
                    }
                    catch (FormatException) { }

                    reportError($"failed to bind address {value}");
                }
                return default(None);
            };
            return new BindStringArgVisitor(func, reportError);
        }

        public static ContractInvocation BindFileUriArgs(ContractInvocation invocation, Action<string>? reportError = null, IFileSystem? fileSystem = null)
        {
            fileSystem ??= defaultFileSystem.Value;
            reportError ??= _ => { };
            return BindFileUriArgs(invocation, fileSystem, reportError);
        }

        internal static ContractInvocation BindFileUriArgs(ContractInvocation invocation, IFileSystem fileSystem, Action<string> reportError)
        {
            var binder = CreateFileUriBinder(fileSystem, reportError);
            return BindArguments(invocation, binder);
        }

        internal static BindStringArgVisitor CreateFileUriBinder(IFileSystem fileSystem, Action<string> reportError)
        {
            BindingFunc func = (value, reportError) =>
            {
                const string FILE_URI = "file://";
                if (value.StartsWith(FILE_URI))
                {
                    var path = fileSystem.NormalizePath(value.Substring(FILE_URI.Length));
                    path = !fileSystem.Path.IsPathFullyQualified(path)
                        ? fileSystem.Path.GetFullPath(path, fileSystem.Directory.GetCurrentDirectory())
                        : path;

                    if (!fileSystem.File.Exists(path))
                    {
                        reportError($"{value} not found");
                    }
                    else
                    {
                        try
                        {
                            return PrimitiveContractArg.FromArray(() => fileSystem.File.ReadAllBytes(path));
                        }
                        catch (Exception ex)
                        {
                            reportError(ex.Message);
                        }
                    }
                }
                return default(None);
            };
            return new BindStringArgVisitor(func, reportError);
        }

        public static ContractInvocation BindStringArgs(ContractInvocation invocation, Action<string>? reportError = null)
        {
            reportError ??= _ => { };
            var binder = CreateStringBinder(reportError);
            return BindArguments(invocation, binder);
        }

        internal static BindStringArgVisitor CreateStringBinder(Action<string> reportError)
        {
            BindingFunc func = (value, reportError) =>
            {
                // TODO: Should we support base64 encocding here?
                //       "data:application/octet-stream;base64," would be appropriate prefix,
                //       but man that is LONG

                if (PrimitiveContractArg.TryFromHexString(value, out var arg))
                {
                    return arg;
                }

                try
                {
                    return PrimitiveContractArg.FromString(value);
                }
                catch (System.Exception ex)
                {
                    reportError(ex.Message);
                }

                return default(None);
            };
            return new BindStringArgVisitor(func, reportError);
        }

        static ContractInvocation BindArguments(ContractInvocation invocation, BindStringArgVisitor visitor)
        {
            var args = invocation.Args.Update(visitor.NullCheckVisit);
            return ReferenceEquals(args, invocation.Args)
                ? invocation
                : invocation with { Args = args };
        }

        static bool TryConvertContractHash(string contract, TryConvert<UInt160>? tryGetContractHash, out UInt160 hash)
        {
            if (tryGetContractHash is not null
                && tryGetContractHash(contract, out var _hash))
            {
                hash = _hash;
                return true;
            }

            if (Neo.SmartContract.Native.NativeContract.Contracts.TryFind(
                    nc => nc.Name.Equals(contract, StringComparison.OrdinalIgnoreCase),
                    out var result))
            {
                hash = result.Hash;
                return true;
            }

            if (UInt160.TryParse(contract, out hash))
            {
                return true;
            }

            hash = UInt160.Zero;
            return false;
        }
    }
}
