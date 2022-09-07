using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Neo.BlockchainToolkit.Models
{
    public partial class ToolkitChain
    {
        public uint Network { get; set; }
        public byte AddressVersion { get; set; } = ProtocolSettings.Default.AddressVersion;
        public IList<ConsensusNode> ConsensusNodes { get; set; } = new List<ConsensusNode>();
        public IList<ToolkitWallet> Wallets { get; set; } = new List<ToolkitWallet>();
        public IDictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        public static ToolkitChain ReadJson(JsonElement json)
        {
            var network = (json.TryGetProperty("network", out var prop)
                ? prop : json.GetProperty("magic")).GetUInt32();
            var addressVersion = json.GetProperty("address-version").GetByte();
            var protocolSettings = Neo.ProtocolSettings.Default with { AddressVersion = addressVersion, Network = network };

            var nodes = json.GetProperty("consensus-nodes").EnumerateArray().Select(j => ConsensusNode.ReadJson(j, protocolSettings));
            var wallets = json.TryGetProperty("wallets", out prop)
                ? prop.EnumerateArray().Select(j => ToolkitWallet.ReadJson(j, protocolSettings))
                : Enumerable.Empty<ToolkitWallet>();
            var settings = json.TryGetProperty("settings", out prop)
                ? prop.EnumerateObject().Select(p => KeyValuePair.Create(p.Name, p.Value.GetString() ?? throw new JsonException()))
                : Enumerable.Empty<KeyValuePair<string, string>>();
            
            return new ToolkitChain
            {
                Network = network,
                AddressVersion = addressVersion,
                ConsensusNodes = nodes.ToList(),
                Wallets = wallets.ToList(),
                Settings = settings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        public void WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteNumber("network", Network);
            writer.WriteNumber("address-version", AddressVersion);
            writer.WriteStartArray("consensus-nodes");
            foreach (var node in ConsensusNodes)
            {
                node.WriteJson(writer);
            }
            writer.WriteEndArray();
            if (Wallets.Count > 0)
            {
                writer.WriteStartArray("wallets");
                foreach (var wallet in Wallets)
                {
                    wallet.WriteJson(writer);
                }
                writer.WriteEndArray();
            }
            if (Settings.Count > 0)
            {
                writer.WriteStartObject("settings");
                foreach (var (key, value) in Settings)
                {
                    writer.WriteString(key, value);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
    }
}
