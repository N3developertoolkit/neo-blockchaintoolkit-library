using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.Persistence;
using OneOf;

namespace Neo.BlockchainToolkit.Persistence
{
    public class NullStore : IReadOnlyStore
    {
        public static readonly NullStore Instance = new NullStore();

        private NullStore() { }

        public bool Contains(byte[]? key) => false;
        public byte[]? TryGet(byte[]? key) => null;
        public IEnumerable<(byte[] Key, byte[]? Value)> Seek(byte[] key, SeekDirection direction)
            => Enumerable.Empty<(byte[], byte[]?)>();
    }
}