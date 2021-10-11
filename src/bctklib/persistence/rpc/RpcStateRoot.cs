using System.Linq;
using Neo;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;

namespace Neo.BlockchainToolkit.Persistence.RPC
{
    public class RpcStateRoot
    {
        public byte Version;
        public uint Index;
        public UInt256 RootHash = UInt256.Zero;
        public Witness? Witness;

        public static RpcStateRoot FromJson(JObject json)
        {
            return new RpcStateRoot
            {
                Version = (byte)json["version"].AsNumber(),
                Index = (uint)json["index"].AsNumber(),
                RootHash = UInt256.Parse(json["roothash"].AsString()),
                Witness = ((JArray)json["witnesses"])
                    .Select(p => Neo.Network.RPC.Utility.WitnessFromJson(p))
                    .FirstOrDefault()
            };
        }
    }
}
