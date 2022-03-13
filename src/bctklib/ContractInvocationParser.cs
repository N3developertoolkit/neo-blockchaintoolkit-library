
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using System.Numerics;
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

    public abstract record ContractArg
    {
        [return: MaybeNull]
        public abstract TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor);
    }

    public record NullContractArg : ContractArg
    {
        public readonly static NullContractArg Null = new NullContractArg();

        [return: MaybeNull]
        public override TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor)
        {
            return visitor.VisitNull(this);
        }
    }

    public record PrimitiveContractArg(PrimitiveArg Value) : ContractArg
    {
        public static PrimitiveContractArg FromArray(Func<byte[]> makeArray)
        {
            var array = makeArray();
            var ia = System.Runtime.CompilerServices.Unsafe.As<byte[], ImmutableArray<byte>>(ref array);
            return new PrimitiveContractArg(ia);
        }

        public static PrimitiveContractArg FromString(string value)
            => FromArray(() => Utility.StrictUTF8.GetBytes(value));

        public static bool TryFromHexString(string hexString, [MaybeNullWhen(false)] out PrimitiveContractArg arg)
        {
            if (hexString.StartsWith("0x"))
            {
                try
                {
                    arg = FromArray(() => Convert.FromHexString(hexString.AsSpan(2)));
                    return true;
                }
                catch (FormatException) { }
            }

            arg = default;
            return false;
        }

        public static bool TryFromBase64String(ReadOnlyMemory<char> base64, [MaybeNullWhen(false)] out PrimitiveContractArg arg)
        {
            var buffer = new byte[GetLength(base64)];
            if (Convert.TryFromBase64Chars(base64.Span, buffer, out var written))
            {
                arg = FromArray(() => buffer);
                return true;
            }

            arg = default;
            return false;

            static int GetLength(ReadOnlyMemory<char> base64)
            {
                if (base64.IsEmpty) return 0;

                var characterCount = base64.Length;
                var paddingCount = 0;
                if (base64.Span[characterCount - 1] == '=') paddingCount++;
                if (base64.Span[characterCount - 2] == '=') paddingCount++;

                return (3 * (characterCount / 4)) - paddingCount;
            }
        }

        public static PrimitiveContractArg FromSerializable(ISerializable value)
            => FromArray(() => value.ToArray());

        [return: MaybeNull]
        public override TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor)
        {
            return visitor.VisitPrimitive(this);
        }
    }

    public record ArrayContractArg(IReadOnlyList<ContractArg> Values) : ContractArg
    {
        [return: MaybeNull]
        public override TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor)
        {
            return visitor.VisitArray(this);
        }
    }

    public record MapContractArg(IReadOnlyList<(ContractArg key, ContractArg value)> Values) : ContractArg
    {
        [return: MaybeNull]
        public override TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor)
        {
            return visitor.VisitMap(this);
        }
    }

    public readonly record struct ContractInvocation(
        OneOf<UInt160, string> Contract,
        string Operation,
        CallFlags CallFlags,
        IReadOnlyList<ContractArg> Args);

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
            }
            return valid;
        }

        public static IEnumerable<ContractInvocation> Bind(IEnumerable<ContractInvocation> invocations,
            ICollection<Diagnostic> diagnostics,
            byte addressVersion = 0,
            TryConvert<UInt160>? tryGetAccount = null,
            TryConvert<UInt160>? tryGetContract = null,
            IFileSystem? fileSystem = null)
        {
            Action<string> reportError = msg => diagnostics.Add(Diagnostic.Error(msg));
            addressVersion = addressVersion == 0
                ? Neo.ProtocolSettings.Default.AddressVersion
                : addressVersion;

            var contractBinder = CreateContractBinder(tryGetContract, reportError);
            var addressBinder = CreateAddressBinder(tryGetAccount, addressVersion, reportError);
            var fileUriBinder = CreateFileUriBinder(fileSystem ?? new FileSystem(), reportError);
            var stringBinder = CreateStringBinder(reportError);

            return invocations
                .Select(i => BindContract(i, tryGetContract, reportError))
                .Select(i => BindArguments(i, contractBinder))
                .Select(i => BindArguments(i, addressBinder))
                .Select(i => BindArguments(i, fileUriBinder))
                .Select(i => BindArguments(i, stringBinder));
        }

        static ContractInvocation BindContract(ContractInvocation invocation, TryConvert<UInt160>? tryGetContractHash, Action<string> reportError)
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

        static ContractInvocation BindArguments(ContractInvocation invocation, BindStringArgVisitor visitor)
        {
            var args = invocation.Args.Update(visitor.NullCheckVisit);
            return ReferenceEquals(args, invocation.Args)
                ? invocation
                : invocation with { Args = args };
        }

        static BindStringArgVisitor CreateContractBinder(TryConvert<UInt160>? tryGetContractHash, Action<string> reportError)
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

        static BindStringArgVisitor CreateAddressBinder(TryConvert<UInt160>? tryGetAddress, byte addressVersion, Action<string> reportError)
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

        static BindStringArgVisitor CreateFileUriBinder(IFileSystem fileSystem, Action<string> reportError)
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

        static BindStringArgVisitor CreateStringBinder(Action<string> reportError)
        {
            BindingFunc func = (value, reportError) =>
            {
                // TODO: Should we support base64 encocding here?
                //       "data:application/octet-stream;base64," would be appropriate prefix

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

        static bool TryConvertContractHash(string contract, TryConvert<UInt160>? tryGetContractHash, out UInt160 hash)
        {
            if (tryGetContractHash is not null
                && tryGetContractHash(contract, out var _hash))
            {
                hash = _hash;
                return true;
            }

            if (Neo.SmartContract.Native.NativeContract.Contracts.TryFind(
                    nc => nc.Name.Equals(contract, StringComparison.InvariantCultureIgnoreCase),
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
