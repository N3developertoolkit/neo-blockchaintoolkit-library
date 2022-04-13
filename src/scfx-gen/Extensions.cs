using Microsoft.CodeAnalysis;
using Neo.BlockchainToolkit.Models;

static class Extensions
{
    public static string AsSource(this ContractType type) 
        => type switch
        {
            PrimitiveContractType p => $"PrimitiveContractType.{p.Type}",
            SymbolContractType s => $"NativeStructs.{s.Symbol.Name}",
            ArrayContractType a => $"new ArrayContractType({a.Type.AsSource()})",
            UnspecifiedContractType => "ContractType.Unspecified",
            InteropContractType i => $"new InteropContractType(\"{i.Type}\")",
            _ => throw new NotImplementedException($"{nameof(AsSource)} {type.GetType().Name}"),
        };

    public static string AsString(this ContractType type) 
        => type switch
        {
            PrimitiveContractType p => $"PrimitiveContractType.{p.Type}",
            SymbolContractType s => $"NativeStructs.{s.Symbol.Name}",
            ArrayContractType a => $"new ArrayContractType({a.Type.AsSource()})",
            UnspecifiedContractType => "ContractType.Unspecified",
            InteropContractType i => $"new InteropContractType(\"{i.Type}\")",
            _ => throw new NotImplementedException($"{nameof(AsSource)} {type.GetType().Name}"),
        };

    public static INamedTypeSymbol FindType(this Compilation compilation, string name)
        => compilation.GetTypeByMetadataName(name) ?? throw new Exception($"{name} type not found");

    public static IEnumerable<IFieldSymbol> GetAllFields(this ITypeSymbol @this)
    {
        if (@this.SpecialType == SpecialType.System_Object) return Enumerable.Empty<IFieldSymbol>();

        var baseFields = @this.IsReferenceType && @this.BaseType is not null
            ? GetAllFields(@this.BaseType)
            : Enumerable.Empty<IFieldSymbol>();
        return baseFields
            .Concat(@this.GetMembers().OfType<IFieldSymbol>());
    }
}
