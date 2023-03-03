using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit.Models
{
    public record ToolkitConsensusNode(ToolkitWallet Wallet, ushort TcpPort, ushort WebSocketPort, ushort RpcPort)
    {
        public static ToolkitConsensusNode Parse(JObject json, ProtocolSettings settings)
        {
            var tcp = json.Value<ushort>("tcp-port");
            var ws = json.Value<ushort>("ws-port");
            var rpc = json.Value<ushort>("rpc-port");
            var walletJson = (json["wallet"] as JObject) ?? throw new JsonException("invalid wallet property");
            var wallet = ToolkitWallet.Parse(walletJson, settings);
            return new(wallet, tcp, ws, rpc);
        }

        public void WriteJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            if (TcpPort != 0) writer.WriteProperty("tcp-port", TcpPort);
            if (WebSocketPort != 0) writer.WriteProperty("ws-port", WebSocketPort);
            writer.WriteProperty("rpc-port", RpcPort);
            writer.WritePropertyName("wallet");
            Wallet.WriteJson(writer);
            writer.WriteEndObject();
        }
    }
}
