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

    public abstract record ContractType();

    public record PrimitiveContractType(PrimitiveType Type) : ContractType;

    public record StructContractType(string Name, IReadOnlyList<(string Name, ContractType Type)> Fields) : ContractType;

    public record MapContractType(PrimitiveType KeyType, ContractType ValueType) : ContractType;
}