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

    // TODO: These type definitions are not quite ready for prime time, but will eventually be needed in order
    //       to encode other Neo core types like ContractManifest as well as to support the full range
    //       of higher level types contract authors leverage from mainstream programing languages but
    //       are elided at runtime.

    // The type definitions to be considered include:
    //  * a type to represent a NeoVM Map. While the runtime does not enforce consistency of map key or value types,
    //      most developers using a statically typed programming language like C# will use compile time generics
    //      to create maps with homogeneous keys and values
    //  * A type to represent a NeoVM Array. NeoVM uses arrays under the hood for both homogeneous collections
    //      as well as heterogeneous structs. The compiler can differentiate between these two uses
    //      even though type type information is elided from the runtime
    //  * An "Unspecified" type. There will be times where the type information cannot be calculated.
    //      Today, in the debugger, a null ContractType in cases where the type infomation is missing.
    //      However, having a sentinel unspecified type will simplify null checking logic in the debugger
    //      and other tools
    //  * A primitive "byte" type. While NeoVM doesn't have a runtime type for a single bytes, the debugger
    //      watch window allows the developer to specify a single byte of a buffer or byte string. Since there
    //      is no way to encode "a single byte", the debugger treats byte a 1 byte string and has
    //      special case code in the ByteArrayContainer to render a single byte strings. Having a primitive
    //      byte type would enable the debugger to eliminate this special case. Note, the debugger uses
    //      ContractTypes to flow type info thru the process of parsing watch expressions. There is no 
    //      easy way to create debugger specific type information like "this is a single byte". So we 
    //      have to continue to use the single byte array special case (which can impact scenarios where 
    //      a developer's code actually has a single byte array), add a byte field to PrimitiveType
    //      or add custom, debugger specific mechanism. Maybe the debugger can define it's own ContractType
    //      subclass for "single byte"? I actually like that idea a lot. No workaround and no change to the 
    //      shared contract type model.

    // public record MapContractType(PrimitiveType KeyType, ContractType ValueType) : ContractType;
    // public record ArrayContractType(ContractType Type) : ContractType;
    // public record UnspecifiedContractType() : ContractType
    // {
    //     public readonly static UnspecifiedContractType Unspecified = new UnspecifiedContractType();
    // }
}