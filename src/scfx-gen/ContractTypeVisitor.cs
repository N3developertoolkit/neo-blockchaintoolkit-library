using Microsoft.CodeAnalysis;
using Neo.BlockchainToolkit.Models;

record InteropSymbolContractType(INamedTypeSymbol Symbol) : ContractType;
record SymbolContractType(INamedTypeSymbol Symbol) : ContractType;
record VoidContractType() : ContractType
{
    public readonly static VoidContractType Void = new VoidContractType();
}

class ContractTypeVisitor : SymbolVisitor<ContractType>
{
    public readonly INamedTypeSymbol ECPoint;
    public readonly INamedTypeSymbol ByteString;
    public readonly INamedTypeSymbol UInt160;
    public readonly INamedTypeSymbol UInt256;
    public readonly INamedTypeSymbol BigInteger;
    public readonly INamedTypeSymbol Map;
    public readonly INamedTypeSymbol List;
    public readonly INamedTypeSymbol ApiInterface;
    public readonly IReadOnlySet<INamedTypeSymbol> NeoPrimitives;

    readonly HashSet<ISymbol> unresolvedTypes = new(SymbolEqualityComparer.Default);
    public IReadOnlySet<ISymbol> UnresolvedTypes => unresolvedTypes;

    public ContractTypeVisitor(Compilation compilation)
    {
        // TODO: Add Address type

        ApiInterface = compilation.FindType("Neo.SmartContract.Framework.IApiInterface");
        BigInteger = compilation.FindType("System.Numerics.BigInteger");
        ByteString = compilation.FindType("Neo.SmartContract.Framework.ByteString");
        ECPoint = compilation.FindType("Neo.Cryptography.ECC.ECPoint");
        List = compilation.FindType("Neo.SmartContract.Framework.List`1");
        Map = compilation.FindType("Neo.SmartContract.Framework.Map`2");
        UInt160 = compilation.FindType("Neo.UInt160");
        UInt256 = compilation.FindType("Neo.UInt256");

        NeoPrimitives = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { ECPoint, ByteString, UInt160, UInt256 };
    }

    public ContractType Resolve(ISymbol symbol) => Visit(symbol) ?? LogUnresolved(symbol);

    UnspecifiedContractType LogUnresolved(ISymbol symbol)
    {
        unresolvedTypes.Add(symbol);
        return UnspecifiedContractType.Unspecified;
    }

    public override ContractType? VisitNamedType(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind == TypeKind.Enum) return PrimitiveContractType.Integer;

        return symbol.SpecialType switch
        {
            SpecialType.System_Boolean => PrimitiveContractType.Boolean,
            SpecialType.System_String => PrimitiveContractType.String,
            SpecialType.System_Void => VoidContractType.Void,
            SpecialType.System_Char => PrimitiveContractType.Integer,
            SpecialType.System_Byte => PrimitiveContractType.Integer,
            SpecialType.System_SByte => PrimitiveContractType.Integer,
            SpecialType.System_Int16 => PrimitiveContractType.Integer,
            SpecialType.System_Int32 => PrimitiveContractType.Integer,
            SpecialType.System_Int64 => PrimitiveContractType.Integer,
            SpecialType.System_UInt16 => PrimitiveContractType.Integer,
            SpecialType.System_UInt32 => PrimitiveContractType.Integer,
            SpecialType.System_UInt64 => PrimitiveContractType.Integer,
            SpecialType.System_Object => UnspecifiedContractType.Unspecified,
            SpecialType.None => ConvertSymbol(symbol),
            _ => LogUnresolved(symbol)
        };
    }

    public override ContractType? VisitArrayType(IArrayTypeSymbol symbol)
    {
        if (symbol.ElementType.SpecialType == SpecialType.System_Byte)
            return PrimitiveContractType.ByteArray;

        var elementType = Visit(symbol.ElementType) ?? UnspecifiedContractType.Unspecified;
        return new ArrayContractType(elementType);
    }

    ContractType? ConvertSymbol(INamedTypeSymbol symbol)
    {
        Func<ISymbol?, ISymbol?, bool> equals = SymbolEqualityComparer.Default.Equals;

        if (equals(symbol, BigInteger)) return PrimitiveContractType.Integer;
        if (equals(symbol, ByteString)) return PrimitiveContractType.ByteArray;
        if (equals(symbol, ECPoint)) return PrimitiveContractType.PublicKey;
        if (equals(symbol, UInt160)) return PrimitiveContractType.Hash160;
        if (equals(symbol, UInt256)) return PrimitiveContractType.Hash256;

        if (symbol.AllInterfaces.Any(i => equals(i, ApiInterface)))
            return new InteropSymbolContractType(symbol);

        if (symbol.IsGenericType)
        {
            if (equals(Map, symbol.ConstructedFrom))
            {
                var key = Visit(symbol.TypeArguments[0]) as PrimitiveContractType;
                if (key is null) throw new Exception("Invalid Map Key Type");

                var value = Visit(symbol.TypeArguments[1]) ?? UnspecifiedContractType.Unspecified;
                return new MapContractType(key.Type, value);
            }

            if (equals(Map, symbol.ConstructedFrom))
            {
                var type = Visit(symbol.TypeArguments[0]) ?? UnspecifiedContractType.Unspecified;
                return new ArrayContractType(type);
            }

            return LogUnresolved(symbol);
        }

        return new SymbolContractType(symbol);
    }
}