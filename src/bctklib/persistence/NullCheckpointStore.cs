using System;
using System.Collections.Generic;
using System.Linq;
using Neo.BlockchainToolkit.Models;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public class NullCheckpointStore : ICheckpointStore
    {
        public ProtocolSettings Settings { get; }

        public NullCheckpointStore(ExpressChain? chain)
            : this(chain?.Network, chain?.AddressVersion)
        {
        }

        public NullCheckpointStore(uint? network = null, byte? addressVersion = null)
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