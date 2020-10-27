using System;
using System.Collections.Generic;
using System.Linq;
using Neo.IO.Caching;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public class NullReadOnlyStore : IReadOnlyStore
    {
        private static readonly Lazy<NullReadOnlyStore> _instance = new Lazy<NullReadOnlyStore>(() => new NullReadOnlyStore());
        public static NullReadOnlyStore Instance => _instance.Value;

        private NullReadOnlyStore()
        {
        } 
        
        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte table, byte[] key, SeekDirection direction)
            => Enumerable.Empty<(byte[] Key, byte[] Value)>();

        bool IReadOnlyStore.Contains(byte table, byte[] key) => false;

        byte[]? IReadOnlyStore.TryGet(byte table, byte[]? key) => null;
    }
}
