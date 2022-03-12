
using System;
using System.Collections.Generic;
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

    public record ContractArg;

    public record NullContractArg : ContractArg
    {
        public readonly static NullContractArg Null = new NullContractArg(); 
    }

    public record PrimitiveContractArg(PrimitiveArg Value) : ContractArg
    {
        public static explicit operator PrimitiveContractArg(byte[] array)
            => new PrimitiveContractArg((ReadOnlyMemory<byte>)array);

        public static PrimitiveContractArg FromString(string value)
            => (PrimitiveContractArg)Utility.StrictUTF8.GetBytes(value);
        
        public static PrimitiveContractArg FromSerializable(ISerializable value)
            => (PrimitiveContractArg)value.ToArray();
    }

    public record ArrayContractArg(IReadOnlyList<ContractArg> Values) : ContractArg;
    public record MapContractArg(IReadOnlyList<(ContractArg key, ContractArg value)> Values) : ContractArg;
    
    public readonly record struct ContractInvocation(
        OneOf<UInt160, string> Contract,
        string Operation,
        IReadOnlyList<ContractArg> Args);

    public static class ContractInvocationParser
    {
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
                    ? ParseArgs(jsonArgs).ToArray()
                    : Array.Empty<ContractArg>();
            return new ContractInvocation(contract, operation, args);
        }

        public static IEnumerable<ContractArg> ParseArgs(JToken jsonArgs)
        {
            if (jsonArgs is JArray argsArray)
            {
                foreach (var arg in argsArray)
                {
                    yield return ParseArg(arg);
                }
            }
            else
            {
                yield return ParseArg(jsonArgs);
            }
        }

        public static ContractArg ParseArg(JToken json)
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
            bool valid = true;
            foreach (var invocation in invocations)
            {
                valid &= Validate(invocation, diagnostics);
            }
            return valid;
        }

        public static bool Validate(ContractInvocation invocation, ICollection<Diagnostic> diagnostics)
        {
            bool valid = true;
            if (invocation.Contract.IsT1)
            {
                valid = false;
                diagnostics.Add(Diagnostic.Error($"Unbound contract hash {invocation.Contract.AsT1}"));
            }

            if (string.IsNullOrEmpty(invocation.Operation))
            {
                valid = false;
                diagnostics.Add(Diagnostic.Error($"Missing opertion"));
            }

            foreach (var arg in invocation.Args)
            {
                valid &= Validate(arg, diagnostics);
            }

            return valid;
        }

        public static bool Validate(ContractArg arg, ICollection<Diagnostic> diagnostics)
        {
            return arg switch
            {
                NullContractArg => true,
                PrimitiveContractArg prim => ValidatePrimitive(prim, diagnostics),
                ArrayContractArg array => ValidateArray(array, diagnostics),
                MapContractArg map => ValidateMap(map, diagnostics),
                _ => false,
            };

            static bool ValidatePrimitive(PrimitiveContractArg arg, ICollection<Diagnostic> diagnostics)
            {
                if (arg.Value.IsT3)
                {
                    diagnostics.Add(Diagnostic.Error($"Unbound string arg '{arg.Value.AsT3}"));
                    return false;
                }
                return true;
            }

            static bool ValidateArray(ArrayContractArg array, ICollection<Diagnostic> diagnostics)
            {
                bool valid = true;
                foreach (var arg in array.Values)
                {
                    valid &= Validate(arg, diagnostics);
                }
                return valid;
            }

            static bool ValidateMap(MapContractArg map, ICollection<Diagnostic> diagnostics)
            {
                bool valid = true;
                foreach (var kvp in map.Values)
                {
                    valid &= Validate(kvp.key, diagnostics);
                    valid &= Validate(kvp.value, diagnostics);
                    if (kvp.key is not PrimitiveContractArg)
                    {
                        diagnostics.Add(Diagnostic.Error("Map keys must be primitive types"));
                        valid = false;
                    }
                }

                return valid;
            }
        }

    }
}
