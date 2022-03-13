
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        public delegate bool TryGetContractHash(string value, out UInt160 account);
        public delegate bool TryGetAddress(string value, out UInt160 account);

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

        internal static IReadOnlyList<T> Update<T>(IReadOnlyList<T> items, Func<T, T> update)
            where T : class
        {
            // Lazily create updatedItems list when we first encounter an updated item
            List<T>? updatedItems = null;
            for (int i = 0; i < items.Count; i++)
            {
                // Potentially update the item
                var updatedItem = update(items[i]);
 
                // if we haven't already got an updatedItems list
                // check to see if the object returned from update
                // is different from the one we passed in 
                if (updatedItems is null
                    && !object.ReferenceEquals(updatedItem, items[i]))
                {
                    // If this is the first modified updatedItem, 
                    // create the updatedItems list and add all the
                    // previously processed and unmodified items 
 
                    updatedItems = new List<T>(items.Count);
                    for (int j = 0; j < i; j++)
                    {
                        updatedItems.Add(items[j]);
                    }
                }

                // if the updated items list exists, add the updatedItem to it
                // (modified or not) 
                if (updatedItems is not null)
                {
                    updatedItems.Add(updatedItem);
                }
            }

            // updateItems will be null if there were no modifications
            return updatedItems ?? items;
        }


        public static IEnumerable<ContractInvocation> BindContracts(IEnumerable<ContractInvocation> invocations, TryGetContractHash? tryGetContractHash, ICollection<Diagnostic> diagnostics)
        {
            foreach (var invocation in invocations)
            {
                yield return invocation;
            }
        }

        static bool TryPickContractHash(string contract, TryGetContractHash? tryGetContractHash, out UInt160 hash)
        {
            if (tryGetContractHash is not null
                && tryGetContractHash(contract, out hash))
            {
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
