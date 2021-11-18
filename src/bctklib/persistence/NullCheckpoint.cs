using System.Collections.Generic;
using System.Linq;
using Neo.BlockchainToolkit.Models;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public class NullCheckpoint : ICheckpoint
    {
        public ProtocolSettings Settings { get; }

        public NullCheckpoint(ExpressChain? chain) : this(chain?.Network, chain?.AddressVersion)
        {
        }

        public NullCheckpoint(uint? network, byte? addressVersion)
        {
            this.Settings = ProtocolSettings.Default with
            {
                Network = network ?? ProtocolSettings.Default.Network,
                AddressVersion = addressVersion ?? ProtocolSettings.Default.AddressVersion,
            };
        }

        public IEnumerable<(byte[] Key, byte[]? Value)> Seek(byte[] key, SeekDirection direction)
            => Enumerable.Empty<(byte[], byte[]?)>();
        public byte[]? TryGet(byte[] key) => null;
        public bool Contains(byte[] key) => false;
    }
}