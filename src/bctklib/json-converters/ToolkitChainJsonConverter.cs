using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Neo.BlockchainToolkit.Models;

namespace Neo.BlockchainToolkit.JsonConverters
{
    public class ToolkitChainJsonConverter : JsonConverter<ToolkitChain>
    {
        public override ToolkitChain Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var json = JsonElement.ParseValue(ref reader);
            return ReadJson(json);
        }

        public static ToolkitChain ReadJson(JsonElement json)
        {
            var network = (json.TryGetProperty("network", out var prop)
                ? prop : json.GetProperty("magic")).GetUInt32();
            var addressVersion = json.GetProperty("address-version").GetByte();
            var protocolSettings = Neo.ProtocolSettings.Default with { AddressVersion = addressVersion, Network = network };

            var nodes = json.GetProperty("consensus-nodes").EnumerateArray().Select(j => ReadConsensusNodeJson(j, protocolSettings));
            var wallets = json.TryGetProperty("wallets", out prop)
                ? prop.EnumerateArray().Select(j => ToolkitWalletJsonConverter.ReadJson(j, protocolSettings))
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

            static ToolkitChain.ConsensusNode ReadConsensusNodeJson(JsonElement json, ProtocolSettings settings)
            {
                var tcpPort = json.GetProperty("tcp-port").GetUInt16();
                var wsPort = json.GetProperty("ws-port").GetUInt16();
                var rpcPort = json.GetProperty("rpc-port").GetUInt16();
                var wallet = ToolkitWalletJsonConverter.ReadJson(json.GetProperty("wallet"), settings);
                return new ToolkitChain.ConsensusNode(wallet)
                {
                    TcpPort = tcpPort,
                    WebSocketPort = wsPort,
                    RpcPort = rpcPort,
                };
            }

        }

        public override void Write(Utf8JsonWriter writer, ToolkitChain value, JsonSerializerOptions options)
        {
            WriteJson(writer, value);
        }

        public static void WriteJson(Utf8JsonWriter writer, ToolkitChain value)
        {
            writer.WriteStartObject();
            writer.WriteNumber("network", value.Network);
            writer.WriteNumber("address-version", value.AddressVersion);
            writer.WriteStartArray("consensus-nodes");
            foreach (var node in value.ConsensusNodes)
            {
                WriteConsensusNodeJson(writer, node);
            }
            writer.WriteEndArray();
            if (value.Wallets.Count > 0)
            {
                writer.WriteStartArray("wallets");
                foreach (var wallet in value.Wallets)
                {
                    ToolkitWalletJsonConverter.WriteJson(writer, wallet);
                }
                writer.WriteEndArray();
            }
            if (value.Settings.Count > 0)
            {
                writer.WriteStartObject("settings");
                foreach (var kvp in value.Settings)
                {
                    writer.WriteString(kvp.Key, kvp.Value);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();

            static void WriteConsensusNodeJson(Utf8JsonWriter writer, ToolkitChain.ConsensusNode node)
            {
                writer.WriteNumber("tcp-port", node.TcpPort);
                writer.WriteNumber("ws-port", node.WebSocketPort);
                writer.WriteNumber("rpc-port", node.RpcPort);
                writer.WritePropertyName("wallet");
                ToolkitWalletJsonConverter.WriteJson(writer, node.Wallet);
            }
        }
    }
}
