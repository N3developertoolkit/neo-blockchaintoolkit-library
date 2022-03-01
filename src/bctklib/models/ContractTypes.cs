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

    public record UnspecifiedContractType() : ContractType
    {
        public readonly static UnspecifiedContractType Unspecified = new UnspecifiedContractType();
    }

    public record PrimitiveContractType(PrimitiveType Type) : ContractType
    {
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

    public record StructContractType(string Name, IReadOnlyList<(string Name, ContractType Type)> Fields) : ContractType;

    // public record MapContractType(PrimitiveType KeyType, ContractType ValueType) : ContractType;
    // public record ArrayContractType(ContractType Type) : ContractType;
}