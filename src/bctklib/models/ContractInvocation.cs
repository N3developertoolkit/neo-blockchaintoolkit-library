
using System;
using System.Collections.Generic;
using System.Numerics;
using Neo.IO;
using Neo.SmartContract;
using OneOf;

namespace Neo.BlockchainToolkit.Models
{
    // Note, string is not a valid arg type. Strings need to be converted into byte arrays 
    // by later processing steps. However, the specific conversion of string to byte array
    // is string content and arg binder dependent

    using PrimitiveArg = OneOf<bool, BigInteger, ReadOnlyMemory<byte>, string>;

    public readonly record struct ContractInvocation(
        OneOf<UInt160, string> Contract,
        string Operation,
        CallFlags CallFlags,
        IReadOnlyList<ContractArg> Args);

    public abstract record ContractArg;

    public record NullContractArg : ContractArg
    {
        public readonly static NullContractArg Null = new NullContractArg();
    }

    public record PrimitiveContractArg(PrimitiveArg Value) : ContractArg
    {
        public PrimitiveContractArg(byte[] array) : this((ReadOnlyMemory<byte>)array) { }
        public PrimitiveContractArg(string value) : this(Neo.Utility.StrictUTF8.GetBytes(value)) { }
        public PrimitiveContractArg(ISerializable value) : this(value.ToArray()) { }
    }

    public record ArrayContractArg(IReadOnlyList<ContractArg> Values) : ContractArg;
    public record MapContractArg(IReadOnlyList<KeyValuePair<PrimitiveContractArg, ContractArg>> Values) : ContractArg;
}
