using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.Persistence;
using OneOf;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class CheckpointStorageProvider
    {
        class NullStore : IReadOnlyStore
        {
            public static NullStore Instance = new NullStore();

            private NullStore() { }

            public bool Contains(byte[]? key) => false;
            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] key, SeekDirection direction) => Enumerable.Empty<(byte[], byte[])>();
            public byte[]? TryGet(byte[]? key) => null;
        }
    }
}