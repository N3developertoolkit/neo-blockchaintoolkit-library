using System.Text.Json;

namespace Neo.BlockchainToolkit.Models
{
    public partial class ToolkitChain
    {
        public class ConsensusNode
        {
            public ushort TcpPort { get; set; }
            public ushort WebSocketPort { get; set; }
            public ushort RpcPort { get; set; }
            public ToolkitWallet Wallet { get; set; }

            public ConsensusNode(ToolkitWallet wallet)
            {
                this.Wallet = wallet;
            }

            public static ConsensusNode ReadJson(JsonElement json, ProtocolSettings settings)
            {
                var tcpPort = json.GetProperty("tcp-port").GetUInt16();
                var wsPort = json.GetProperty("ws-port").GetUInt16();
                var rpcPort = json.GetProperty("rpc-port").GetUInt16();
                var wallet = ToolkitWallet.ReadJson(json.GetProperty("wallet"), settings);
                return new ToolkitChain.ConsensusNode(wallet)
                {
                    TcpPort = tcpPort,
                    WebSocketPort = wsPort,
                    RpcPort = rpcPort,
                };
            }

            public void WriteJson(Utf8JsonWriter writer)
            {
                writer.WriteNumber("tcp-port", TcpPort);
                writer.WriteNumber("ws-port", WebSocketPort);
                writer.WriteNumber("rpc-port", RpcPort);
                writer.WritePropertyName("wallet");
                Wallet.WriteJson(writer);
            }
        }
    }
}
