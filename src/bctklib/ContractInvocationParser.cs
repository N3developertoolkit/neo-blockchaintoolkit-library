
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

namespace Neo.BlockchainToolkit
{
    // Note, string is not a valid arg type. Strings need to be converted into byte arrays 
    // by later processing steps. However, the specific conversion of string -> byte array
    // is string content dependent

    using PrimitiveArg = OneOf<bool, BigInteger, ReadOnlyMemory<byte>, string>;

    public abstract record ContractArg
    {
        public abstract TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor);
    }

    public record NullContractArg : ContractArg
    {
        public readonly static NullContractArg Null = new NullContractArg();

        public override TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor)
        {
            return visitor.VisitNull(this);
        }
    }

    public record PrimitiveContractArg(PrimitiveArg Value) : ContractArg
    {
        public static explicit operator PrimitiveContractArg(byte[] array)
            => new PrimitiveContractArg((ReadOnlyMemory<byte>)array);

        public static PrimitiveContractArg FromString(string value)
            => (PrimitiveContractArg)Utility.StrictUTF8.GetBytes(value);

        public static PrimitiveContractArg FromSerializable(ISerializable value)
            => (PrimitiveContractArg)value.ToArray();

        public override TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor)
        {
            return visitor.VisitPrimitive(this);
        }
    }

    public record ArrayContractArg(IReadOnlyList<ContractArg> Values) : ContractArg
    {
        public override TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor)
        {
            return visitor.VisitArray(this);
        }
    }

    public record MapContractArg(IReadOnlyList<(ContractArg key, ContractArg value)> Values) : ContractArg
    {
        public override TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor)
        {
            return visitor.VisitMap(this);
        }
    }

    public readonly record struct ContractInvocation(
        OneOf<UInt160, string> Contract,
        string Operation,
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

            var args = json.TryGetValue("args", out var jsonArgs)
                    ? ParseArgs(jsonArgs)
                    : Array.Empty<ContractArg>();
            return new ContractInvocation(contract, operation, args);
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
                JTokenType.Array => new ArrayContractArg(json.Select(ParseArg).ToImmutableList()),
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
                    new ArrayContractArg(value.Select(ParseArg).ToImmutableList()),
                ContractParameterType.Map =>
                    new MapContractArg(value.Select(ParseMapElement).ToImmutableList()),
                _ => throw new ArgumentException($"invalid type {type}", nameof(json)),
            };

            static PrimitiveContractArg ParseBinary(string value)
            {
                Span<byte> span = stackalloc byte[value.Length / 4 * 3];
                if (Convert.TryFromBase64String(value, span, out var written))
                {
                    return (PrimitiveContractArg)span.Slice(0, written).ToArray();
                }

                return (PrimitiveContractArg)Convert.FromHexString(value);
            }

            static (ContractArg, ContractArg) ParseMapElement(JToken json)
            {
                var key = ParseArg(json["key"] ?? throw new Exception());
                if (key is not PrimitiveContractArg) throw new Exception();
                var value = ParseArg(json["value"] ?? throw new Exception());
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

        public static IEnumerable<ContractInvocation> BindContracts(IEnumerable<ContractInvocation> invocations, TryConvert<UInt160>? tryGetContractHash, ICollection<Diagnostic> diagnostics)
        {
            var visitor = new BindStringArgVisitor(arg => 
            {
                if (arg.Value.TryPickT3(out var @string, out _)
                    && @string.StartsWith("#"))
                {
                    var hashString = @string.Substring(1);

                    if (TryConvertContractHash(hashString, tryGetContractHash, out var uInt160))
                    {
                        return PrimitiveContractArg.FromSerializable(uInt160);
                    }

                    if (UInt256.TryParse(hashString, out var uInt256))
                    {
                        return PrimitiveContractArg.FromSerializable(uInt256);
                    }

                    diagnostics.Add(Diagnostic.Error($"failed to bind contract {@string}"));
                }
                return arg;
            });

            foreach (var invocation in invocations)
            {
                var boundInvocation = invocation.Contract.TryPickT0(out _, out var contract)
                    ? invocation
                    : TryConvertContractHash(StripHash(contract), tryGetContractHash, out var hash)
                        ? invocation with { Contract = hash }
                        : diagnostics.RecordError($"Failed to bind contract {contract}", invocation);


                var args = boundInvocation.Args.Update(visitor.Visit);
                boundInvocation = ReferenceEquals(args, boundInvocation.Args)
                    ? boundInvocation
                    : boundInvocation with { Args = args };

                yield return boundInvocation;
            }

            static string StripHash(string @string) => @string.Length > 0 && @string[0] == '#'
                ? @string.Substring(1) 
                : @string;
        }

        public static IEnumerable<ContractInvocation> BindAddresses(IEnumerable<ContractInvocation> invocations, TryConvert<UInt160>? tryGetAddress, byte addressVersion, ICollection<Diagnostic> diagnostics)
        {
            var visitor = new BindStringArgVisitor(arg => 
            {
                if (arg.Value.TryPickT3(out var @string, out _)
                    && @string.StartsWith("@"))
                {
                    if (TryConvertAddress(@string.Substring(1), tryGetAddress, addressVersion, out var hash))
                    {
                        return PrimitiveContractArg.FromSerializable(hash);
                    }
                    diagnostics.Add(Diagnostic.Error($"failed to bind address {@string}"));
                }
                return arg;
            });

            foreach (var invocation in invocations)
            {
                var args = invocation.Args.Update(visitor.Visit);
                var boundInvocation = ReferenceEquals(args, invocation.Args)
                    ? invocation
                    : invocation with { Args = args };

                yield return boundInvocation;
            }
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

        static bool TryConvertAddress(string address, TryConvert<UInt160>? tryGetAddress, byte addressVersion, out UInt160 hash)
        {
            if (tryGetAddress is not null
                && tryGetAddress(address, out var _hash))
            {
                hash = _hash;
                return true;
            }

            try
            {
                hash = Neo.Wallets.Helper.ToScriptHash(address, addressVersion);
                return true;
            }
            catch (FormatException) { }

            hash = UInt160.Zero;
            return false;
        }
    }
}
