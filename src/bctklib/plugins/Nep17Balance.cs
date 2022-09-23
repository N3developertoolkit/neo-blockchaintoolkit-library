using System.Numerics;

namespace Neo.BlockchainToolkit.Plugins
{
    public record Nep17Balance(
        UInt160 AssetHash,
        string Name,
        string Symbol,
        byte Decimals,
        BigInteger Balance,
        uint LastUpdatedBlock);
}
