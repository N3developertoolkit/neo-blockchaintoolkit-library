using System;
using System.Collections.Generic;
using System.Linq;
using Neo.IO.Caching;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public class NullReadOnlyStore : IExpressReadOnlyStore
    {
        public static NullReadOnlyStore Instance { get; } = new NullReadOnlyStore();

        private NullReadOnlyStore() { }

        byte[]? IExpressReadOnlyStore.TryGet(byte table, byte[]? key) => null;

        bool IExpressReadOnlyStore.Contains(byte table, byte[]? key) => false;

        IEnumerable<(byte[] Key, byte[] Value)> IExpressReadOnlyStore.Seek(byte table, byte[]? key, SeekDirection direction)
            => Enumerable.Empty<(byte[] Key, byte[] Value)>();

        byte[]? IReadOnlyStore.TryGet(byte[] key) => null;

        bool IReadOnlyStore.Contains(byte[] key) => false;

        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte[] key, SeekDirection direction)
            => Enumerable.Empty<(byte[] Key, byte[] Value)>();

    }
}
