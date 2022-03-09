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

        public static bool TryParse(string typeName, IReadOnlyDictionary<string, StructContractType> structs, out ContractType type)
        {
            if (typeName.Length > 0)
            {
                if (typeName.Equals("#Unspecified"))
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

                if (typeName.StartsWith("Array<")
                    && typeName[^1] == '>')
                {
                    if (TryParse(new string(typeName.AsSpan()[6..^1]), structs, out var arrayType))
                    {
                        type = new ArrayContractType(arrayType);
                        return true;
                    }
                }

                if (typeName.StartsWith("Map<")
                    && typeName[^1] == '>')
                {
                    var mapArgs = typeName.AsSpan()[4..^1];
                    var colonIndex = mapArgs.IndexOf(':');
                    if (colonIndex != -1
                        && Enum.TryParse<PrimitiveType>(mapArgs.Slice(0, colonIndex), out var keyType)
                        && TryParse(new string(mapArgs.Slice(colonIndex + 1)), structs, out var valueType))
                    {
                        type = new MapContractType(keyType, valueType);
                        return true;
                    }
                }

                if (typeName.StartsWith("Interop<")
                    && typeName[^1] == '>')
                {
                    var interopArg = typeName.AsSpan()[8..^1];
                    if (false == (interopArg.Contains('#')
                        || interopArg.Contains('<')
                        || interopArg.Contains('>')))
                    {
                        type = new InteropContractType(new string(interopArg));
                        return true;
                    }
                }
                
#if (!SCFXGEN)
                if (typeName.StartsWith("Neo#")
                    && NativeStructs.TryGetType(typeName, out var nativeStruct))
                {
                    type = nativeStruct;
                    return true;
                }
#endif

                if (StructContractType.IsValidName(typeName)
                    && structs.TryGetValue(typeName, out var @struct))
                {
                    type = @struct;
                    return true;
                }
            }

            type = ContractType.Unspecified;
            return false;
        }
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
            if (!name.StartsWith("Neo#") && !IsValidName(name)) throw new ArgumentException(nameof(name));

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