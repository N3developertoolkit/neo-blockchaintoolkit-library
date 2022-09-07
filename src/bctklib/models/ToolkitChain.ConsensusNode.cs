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
        }
    }
}
