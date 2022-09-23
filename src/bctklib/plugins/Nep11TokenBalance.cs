using System;
using System.Numerics;

namespace Neo.BlockchainToolkit.Plugins
{
    public record Nep11TokenBalance(
        ReadOnlyMemory<byte> TokenId,
        BigInteger Balance,
        uint LastUpdatedBlock);
}
