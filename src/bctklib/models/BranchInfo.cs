using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Persistence;
using Neo.IO;
using Neo.Network.RPC;
using Newtonsoft.Json;

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
        public ProtocolSettings ProtocolSettings => ProtocolSettings.Default with
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

        public void WriteJson(JsonWriter writer)
        {
            using var _ = writer.WriteObject();
            writer.WriteProperty("network", network);
            writer.WriteProperty("address-version", addressVersion);
            writer.WriteProperty("index", index);
            writer.WriteProperty("index-hash", $"{indexHash}");
            writer.WriteProperty("root-hash", $"{rootHash}");
            using var __ = writer.WritePropertyObject("contract-map");
            foreach (var (k, v) in contractMap)
            {
                writer.WriteProperty($"{k}", $"{v}");
            }
        }
    }
}
