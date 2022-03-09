using System;
using System.Collections.Generic;

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
        const string ARRAY_PREFIX = "Array<";
        const string MAP_PREFIX = "Map<";
        const string INTEROP_PREFIX = "Interop<";
        protected const string NEO_PREFIX = "Neo#";

#if (!SCFXGEN)
        public static bool TryParse(string typeName, IReadOnlyDictionary<string, StructContractType> structs, out ContractType type)
        {
            if (typeName.Length > 0)
            {
                if (typeName.Equals(UNSPECIFIED))
                {
                    type = ContractType.Unspecified;
                    return true;
                }

                if (typeName[0] == '#'
                    && Enum.TryParse<PrimitiveType>(typeName.AsSpan(1), out var pt))
                {
                    type = PrimitiveContractType.AsContractType(pt);
                    return true;
                }

                if (typeName.StartsWith(ARRAY_PREFIX)
                    && typeName[^1] == '>')
                {
                    var arrayArg = typeName.AsSpan()[ARRAY_PREFIX.Length..^1];

                    // support 'Array<>' syntax for untyped arrays
                    if (arrayArg.IsEmpty)
                    {
                        type = new ArrayContractType(ContractType.Unspecified);
                        return true;
                    }

                    if (TryParse(new string(arrayArg), structs, out var arrayType))
                    {
                        type = new ArrayContractType(arrayType);
                        return true;
                    }
                }

                if (typeName.StartsWith(MAP_PREFIX)
                    && typeName[^1] == '>')
                {
                    var mapArgs = typeName.AsSpan()[MAP_PREFIX.Length..^1];

                    // support 'Map<>' syntax for untyped maps
                    if (mapArgs.IsEmpty)
                    {
                        type = new MapContractType(PrimitiveType.ByteArray, ContractType.Unspecified);
                        return true;
                    }

                    var colonIndex = mapArgs.IndexOf(':');
                    if (colonIndex != -1
                        && Enum.TryParse<PrimitiveType>(mapArgs.Slice(0, colonIndex), out var keyType)
                        && TryParse(new string(mapArgs.Slice(colonIndex + 1)), structs, out var valueType))
                    {
                        type = new MapContractType(keyType, valueType);
                        return true;
                    }
                }

                if (typeName.StartsWith(INTEROP_PREFIX)
                    && typeName[^1] == '>')
                {
                    var interopArg = typeName.AsSpan()[INTEROP_PREFIX.Length..^1];
                    type = new InteropContractType(new string(interopArg));
                    return true;
                }
                
                if (typeName.StartsWith(NEO_PREFIX)
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

            type = ContractType.Unspecified;
            return false;
        }
#endif
    }

    public record UnspecifiedContractType() : ContractType;

    public record PrimitiveContractType(PrimitiveType Type) : ContractType
    {
        public static PrimitiveContractType AsContractType(PrimitiveType type)
            => type switch
            {
                PrimitiveType.Address => PrimitiveContractType.Address,
                PrimitiveType.Boolean => PrimitiveContractType.Boolean,
                PrimitiveType.ByteArray => PrimitiveContractType.ByteArray,
                PrimitiveType.Hash160 => PrimitiveContractType.Hash160,
                PrimitiveType.Hash256 => PrimitiveContractType.Hash256,
                PrimitiveType.Integer => PrimitiveContractType.Integer,
                PrimitiveType.PublicKey => PrimitiveContractType.PublicKey,
                PrimitiveType.Signature => PrimitiveContractType.Signature,
                PrimitiveType.String => PrimitiveContractType.String,
                _ => throw new NotSupportedException($"Unknown {nameof(PrimitiveType)} {type}"),
            };

        public readonly static PrimitiveContractType Address = new PrimitiveContractType(PrimitiveType.Address);
        public readonly static PrimitiveContractType Boolean = new PrimitiveContractType(PrimitiveType.Boolean);
        public readonly static PrimitiveContractType ByteArray = new PrimitiveContractType(PrimitiveType.ByteArray);
        public readonly static PrimitiveContractType Hash160 = new PrimitiveContractType(PrimitiveType.Hash160);
        public readonly static PrimitiveContractType Hash256 = new PrimitiveContractType(PrimitiveType.Hash256);
        public readonly static PrimitiveContractType Integer = new PrimitiveContractType(PrimitiveType.Integer);
        public readonly static PrimitiveContractType PublicKey = new PrimitiveContractType(PrimitiveType.PublicKey);
        public readonly static PrimitiveContractType Signature = new PrimitiveContractType(PrimitiveType.Signature);
        public readonly static PrimitiveContractType String = new PrimitiveContractType(PrimitiveType.String);
    }

    public record StructContractType : ContractType
    {
        public string Name { get; } = string.Empty;
        public IReadOnlyList<(string Name, ContractType Type)> Fields { get; }
            = Array.Empty<(string, ContractType)>();

        public static StructContractType Create(string name, params (string Name, ContractType Type)[] fields)
        {
            return new StructContractType(name, fields);
        }

        public StructContractType(string name, IReadOnlyList<(string Name, ContractType Type)> fields)
        {
            if (!name.StartsWith(NEO_PREFIX) && !IsValidName(name)) throw new ArgumentException(nameof(name));

            this.Name = name;
            this.Fields = fields;
        }

        public static bool IsValidName(ReadOnlySpan<char> typeName)
            => typeName.Length > 0
                && !typeName.Contains('#')
                && !typeName.Contains('<')
                && !typeName.Contains('>');
    }

    public record ArrayContractType(ContractType Type) : ContractType;
    public record MapContractType(PrimitiveType KeyType, ContractType ValueType) : ContractType;
    public record InteropContractType(string Type) : ContractType
    {
        public readonly static InteropContractType Unknown = new InteropContractType(string.Empty);
    }
}