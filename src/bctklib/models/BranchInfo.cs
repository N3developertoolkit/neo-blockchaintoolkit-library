using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        public static void AddConverters(JsonSerializerOptions options)
        {
            options.Converters.Add(UInt160JsonConverter.Instance);
            options.Converters.Add(UInt256JsonConverter.Instance);
        }

        public static Task<BranchInfo> GetBranchInfoAsync(string url, uint index) => GetBranchInfoAsync(new Uri(url), index);

        public static async Task<BranchInfo> GetBranchInfoAsync(Uri url, uint index)
        {
            using var client = new RpcClient(url);
            return await client.GetBranchInfoAsync(index).ConfigureAwait(false);
        }
    }

    public class UInt160JsonConverter : JsonConverter<UInt160>
    {
        public static UInt160JsonConverter Instance = new UInt160JsonConverter();
        public override UInt160? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TryGetBytesFromBase64(out var value)
                ? new UInt160(value)
                : null;
        }

        public override void Write(Utf8JsonWriter writer, UInt160 value, JsonSerializerOptions options)
        {
            writer.WriteBase64StringValue(value.ToArray());
        }
    }

    public class UInt256JsonConverter : JsonConverter<UInt256>
    {
        public static UInt256JsonConverter Instance = new UInt256JsonConverter();

        public override UInt256? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TryGetBytesFromBase64(out var value)
                ? new UInt256(value)
                : null;
        }

        public override void Write(Utf8JsonWriter writer, UInt256 value, JsonSerializerOptions options)
        {
            writer.WriteBase64StringValue(value.ToArray());
        }
    }
}
