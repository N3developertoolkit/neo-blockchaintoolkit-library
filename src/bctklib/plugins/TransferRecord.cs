using System;
using System.Numerics;

namespace Neo.BlockchainToolkit.Plugins
{
    public record TransferRecord(
        ulong Timestamp,
        UInt160 Asset,
        UInt160 From,
        UInt160 To,
        BigInteger Amount,
        uint BlockIndex,
        ushort TransferNotifyIndex,
        UInt256 TxHash,
        ReadOnlyMemory<byte> TokenId);
}
