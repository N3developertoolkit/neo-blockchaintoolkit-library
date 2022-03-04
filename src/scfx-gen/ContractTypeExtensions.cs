using Microsoft.CodeAnalysis;
using Neo.BlockchainToolkit.Models;

static class ContractTypeExtensions
{
    public static string AsSource(this ContractType type) 
        => type switch
        {
            PrimitiveContractType p => $"PrimitiveContractType.{p.Type}",
            SymbolContractType s => $"NeoCoreTypes.{s.Symbol.Name}",
            ArrayContractType a => $"new ArrayContractType({a.Type.AsSource()})",
            UnspecifiedContractType => "UnspecifiedContractType.Unspecified",
            InteropContractType i => $"new InteropContractType(\"{i.Type}\")",
            _ => throw new NotImplementedException($"{nameof(AsSource)} {type.GetType().Name}"),
        };

    public static INamedTypeSymbol FindType(this Compilation compilation, string name)
        => compilation.GetTypeByMetadataName(name) ?? throw new Exception($"{name} type not found");
}
