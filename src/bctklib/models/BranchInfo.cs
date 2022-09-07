using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Persistence;
using Neo.IO;
using Neo.Network.RPC;

namespace Neo.BlockchainToolkit.Models
{
    public readonly record struct BranchInfo(
        uint network,
        byte addressVersion,
        uint index,
        UInt256 indexHash,
        UInt256 rootHash,
        IReadOnlyDictionary<int, UInt160> contractMap)
    {
        public Neo.ProtocolSettings ProtocolSettings => Neo.ProtocolSettings.Default with
        {
            AddressVersion = addressVersion,
            Network = network,
        };

        public static Task<BranchInfo> GetBranchInfoAsync(string url, uint index) => GetBranchInfoAsync(new Uri(url), index);

        public static async Task<BranchInfo> GetBranchInfoAsync(Uri url, uint index)
        {
            using var client = new RpcClient(url);
            return await client.GetBranchInfoAsync(index).ConfigureAwait(false);
        }
    }
}
