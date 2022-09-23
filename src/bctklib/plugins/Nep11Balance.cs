using System.Collections.Generic;

namespace Neo.BlockchainToolkit.Plugins
{
    public record Nep11Balance(
        UInt160 AssetHash,
        string Name,
        string Symbol,
        byte Decimals,
        IReadOnlyList<Nep11TokenBalance> Balances);
}
