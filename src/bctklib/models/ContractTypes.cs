using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Neo.BlockchainToolkit.Models
{
    public enum PrimitiveType : byte
    {
        Boolean,
        Integer,
        ByteArray,
        String,
        Hash160,
        Hash256,
        PublicKey,
        Signature,
        Address,
    }

    public abstract record ContractType()
    {
        public readonly static ContractType Unspecified = new UnspecifiedContractType();

        const string UNSPECIFIED = "#Unspecified";
        const string ARRAY_PREFIX = "#Array<";
        const string MAP_PREFIX = "#Map<";
        const string ITERATOR_PREFIX = "#Iterator<";
        const string INTEROP_PREFIX = "#Interop<";
        protected const string NEO_NAMESPACE = "#Neo.";

        public static bool TryParsePrimitive(ReadOnlySpan<char> typeName, [MaybeNullWhen(false)] out PrimitiveType type)
        {
            if (typeName[0] == '#'
                && Enum.TryParse<PrimitiveType>(typeName[1..], out var primitive))
            {
                type = primitive;
                return true;
            }

            type = default;
            return false;
        }

        public static bool TryParse(string typeName, IReadOnlyDictionary<string, StructContractType> structs, out ContractType type)
        {
            if (typeName.Length > 0)
            {
                if (typeName.Equals(UNSPECIFIED))
                {
                    type = Unspecified;
                    return true;
                }

                if (TryParsePrimitive(typeName, out var primitive))
                {
                    type = PrimitiveContractType.AsContractType(primitive);
                    return true;
                }

                if (typeName.StartsWith(ARRAY_PREFIX)
                    && typeName[^1] == '>')
                {
                    var arrayArg = typeName.AsSpan()[ARRAY_PREFIX.Length..^1];

                    // support 'Array<>' syntax for untyped arrays
                    if (arrayArg.IsEmpty)
                    {
                        type = new ArrayContractType(Unspecified);
                        return true;
                    }

                    var arrayType = TryParse(new string(arrayArg), structs, out var _type)
                        ? _type : Unspecified;
                    type = new ArrayContractType(arrayType);
                    return true;
                }

                if (typeName.StartsWith(MAP_PREFIX)
                    && typeName[^1] == '>')
                {
                    var mapArgs = typeName.AsSpan()[MAP_PREFIX.Length..^1];

                    // support 'Map<>' syntax for untyped maps
                    if (mapArgs.IsEmpty)
                    {
                        type = new MapContractType(PrimitiveType.ByteArray, Unspecified);
                        return true;
                    }

                    var colonIndex = mapArgs.IndexOf(':');
                    if (colonIndex != -1)
                    {
                        var keyType = TryParsePrimitive(mapArgs[..colonIndex], out var _key)
                            ? _key : PrimitiveType.ByteArray;
                        var valueType = TryParse(new string(mapArgs[(colonIndex + 1)..]), structs, out var _value)
                            ? _value : Unspecified;
                        type = new MapContractType(keyType, valueType);
                        return true;
                    }
                }

                if (typeName.StartsWith(ITERATOR_PREFIX)
                    && typeName[^1] == '>')
                {
                    var iteratorArg = typeName.AsSpan()[ITERATOR_PREFIX.Length..^1];

                    // support '#Iterator<>' syntax for untyped iterators
                    if (iteratorArg.IsEmpty)
                    {
                        type = new IteratorContractType(Unspecified);
                        return true;
                    }

                    var iteratorType = TryParse(new string(iteratorArg), structs, out var _type)
                        ? _type : Unspecified;
                    type = new IteratorContractType(iteratorType);
                    return true;
                }

                if (typeName.StartsWith(INTEROP_PREFIX)
                    && typeName[^1] == '>')
                {
                    var interopArg = typeName.AsSpan()[INTEROP_PREFIX.Length..^1];
                    type = new InteropContractType(new string(interopArg));
                    return true;
                }

                if (typeName.StartsWith(NEO_NAMESPACE)
                    && NativeStructs.TryGetType(typeName, out var @struct))
                {
                    type = @struct;
                    return true;
                }

                if (StructContractType.IsValidName(typeName)
                    && structs.TryGetValue(typeName, out @struct))
                {
                    type = @struct;
                    return true;
                }
            }

            type = Unspecified;
            return false;
        }
    }

    public record UnspecifiedContractType() : ContractType;

    public record PrimitiveContractType(PrimitiveType Type) : ContractType
    {
        public static PrimitiveContractType AsContractType(PrimitiveType type)
            => type switch
            {
                PrimitiveType.Address => Address,
                PrimitiveType.Boolean => Boolean,
                PrimitiveType.ByteArray => ByteArray,
                PrimitiveType.Hash160 => Hash160,
                PrimitiveType.Hash256 => Hash256,
                PrimitiveType.Integer => Integer,
                PrimitiveType.PublicKey => PublicKey,
                PrimitiveType.Signature => Signature,
                PrimitiveType.String => String,
                _ => throw new NotSupportedException($"Unknown {nameof(PrimitiveType)} {type}"),
            };

        public readonly static PrimitiveContractType Address = new(PrimitiveType.Address);
        public readonly static PrimitiveContractType Boolean = new(PrimitiveType.Boolean);
        public readonly static PrimitiveContractType ByteArray = new(PrimitiveType.ByteArray);
        public readonly static PrimitiveContractType Hash160 = new(PrimitiveType.Hash160);
        public readonly static PrimitiveContractType Hash256 = new(PrimitiveType.Hash256);
        public readonly static PrimitiveContractType Integer = new(PrimitiveType.Integer);
        public readonly static PrimitiveContractType PublicKey = new(PrimitiveType.PublicKey);
        public readonly static PrimitiveContractType Signature = new(PrimitiveType.Signature);
        public readonly static PrimitiveContractType String = new(PrimitiveType.String);
    }

    public record StructContractType : ContractType
    {
        public string Name { get; } = string.Empty;
        public string ShortName
        {
            get
            {
                var index = Name.LastIndexOf('.');
                return index == -1 ? Name : Name[(index + 1)..];
            }
        }

        public IReadOnlyList<(string Name, ContractType Type)> Fields { get; }
            = Array.Empty<(string, ContractType)>();

        public static StructContractType Create(string name, params (string Name, ContractType Type)[] fields)
        {
            return new StructContractType(name, fields);
        }

        public StructContractType(string name, IReadOnlyList<(string Name, ContractType Type)> fields)
        {
            if (!name.StartsWith(NEO_NAMESPACE) && !IsValidName(name)) throw new ArgumentException(null, nameof(name));

            Name = name;
            Fields = fields;
        }

        public static bool IsValidName(ReadOnlySpan<char> typeName)
            => typeName.Length > 0
                && !typeName.Contains('#')
                && !typeName.Contains('<')
                && !typeName.Contains('>');
    }

    public record ArrayContractType(ContractType Type) : ContractType;

    public record MapContractType(PrimitiveType KeyType, ContractType ValueType) : ContractType;

    public record IteratorContractType(ContractType Type) : ContractType;

    public record InteropContractType(string Type) : ContractType
    {
        public readonly static InteropContractType Unknown = new(string.Empty);
    }
}