using Microsoft.CodeAnalysis;
using Neo.BlockchainToolkit.Models;

static class ContractTypeExtensions
{
    public static string AsSource(this ContractType type) 
        => type switch
        {
            PrimitiveContractType p => $"PrimitiveContractType.{p.Type}",
            SymbolContractType s => s.Symbol.Name,
            ArrayContractType a => $"new ArrayContractType({a.Type.AsSource()})",
            UnspecifiedContractType => "UnspecifiedContractType.Unspecified",
            InteropSymbolContractType i => $"new InteropContractType(\"{i.Symbol.Name}\")",
            _ => throw new NotImplementedException($"{nameof(AsSource)} {type.GetType().Name}"),
        };

    // public static string AsString(this ContractType? type) 
    //     => type switch
    //     {
    //         ArrayContractType a => $"Array<{a.Type.AsString()}>",
    //         InteropContractType i => $"Interop<{i.Symbol}>",
    //         MapContractType m => $"Map<#{m.KeyType}:{m.ValueType.AsString()}>",
    //         PrimitiveContractType p => $"#{p.Type}",
    //         SymbolContractType s => s.Symbol.ToString() ?? throw new Exception(),
    //         UnspecifiedContractType or null => "#Unspecified",
    //         VoidContractType => throw new NotSupportedException($"{nameof(AsString)} {nameof(VoidContractType)}"),
    //         _ => throw new NotImplementedException($"{nameof(AsString)} {type.GetType().Name}"),
    //     };

    public static INamedTypeSymbol FindType(this Compilation compilation, string name)
        => compilation.GetTypeByMetadataName(name) ?? throw new Exception($"{name} type not found");
}
