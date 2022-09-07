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
    }
}
