
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Neo.IO;
using Neo.SmartContract;
using OneOf;

namespace Neo.BlockchainToolkit.Models
{
    // Note, string is not a valid arg type. Strings need to be converted into byte arrays 
    // by later processing steps. However, the specific conversion of string -> byte array
    // is string content dependent

    using PrimitiveArg = OneOf<bool, BigInteger, ReadOnlyMemory<byte>, string>;

    public readonly record struct ContractInvocation(
        OneOf<UInt160, string> Contract,
        string Operation,
        CallFlags CallFlags,
        IReadOnlyList<ContractArg> Args);

    public abstract record ContractArg
    {
        [return: MaybeNull]
        public abstract TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor);
    }

    public record NullContractArg : ContractArg
    {
        public readonly static NullContractArg Null = new NullContractArg();

        [return: MaybeNull]
        public override TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor)
        {
            return visitor.VisitNull(this);
        }
    }

    public record PrimitiveContractArg(PrimitiveArg Value) : ContractArg
    {
        public static PrimitiveContractArg FromArray(Func<byte[]> makeArray)
        {
            var array = makeArray();
            return new PrimitiveContractArg((ReadOnlyMemory<byte>)array);
        }

        public static PrimitiveContractArg FromString(string value)
            => FromArray(() => Neo.Utility.StrictUTF8.GetBytes(value));

        public static bool TryFromHexString(string hexString, [MaybeNullWhen(false)] out PrimitiveContractArg arg)
        {
            if (hexString.StartsWith("0x"))
            {
                try
                {
                    arg = FromArray(() => Convert.FromHexString(hexString.AsSpan(2)));
                    return true;
                }
                catch (FormatException) { }
            }

            arg = default;
            return false;
        }

        public static bool TryFromBase64String(ReadOnlyMemory<char> base64, [MaybeNullWhen(false)] out PrimitiveContractArg arg)
        {
            var buffer = new byte[GetLength(base64)];
            if (Convert.TryFromBase64Chars(base64.Span, buffer, out var written))
            {
                arg = FromArray(() => buffer);
                return true;
            }

            arg = default;
            return false;

            static int GetLength(ReadOnlyMemory<char> base64)
            {
                if (base64.IsEmpty) return 0;

                var characterCount = base64.Length;
                var paddingCount = 0;
                if (base64.Span[characterCount - 1] == '=') paddingCount++;
                if (base64.Span[characterCount - 2] == '=') paddingCount++;

                return (3 * (characterCount / 4)) - paddingCount;
            }
        }

        public static PrimitiveContractArg FromSerializable(ISerializable value)
            => FromArray(() => value.ToArray());

        [return: MaybeNull]
        public override TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor)
        {
            return visitor.VisitPrimitive(this);
        }
    }

    public record ArrayContractArg(IReadOnlyList<ContractArg> Values) : ContractArg
    {
        [return: MaybeNull]
        public override TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor)
        {
            return visitor.VisitArray(this);
        }
    }

    public record MapContractArg(IReadOnlyList<(ContractArg key, ContractArg value)> Values) : ContractArg
    {
        [return: MaybeNull]
        public override TResult Accept<TResult>(ContractInvocationVisitor<TResult> visitor)
        {
            return visitor.VisitMap(this);
        }
    }
}
