using System;
using System.Collections.Generic;

namespace Neo.BlockchainToolkit.Persistence
{
    class ReadOnlyMemoryComparer : IEqualityComparer<ReadOnlyMemory<byte>>, IComparer<ReadOnlyMemory<byte>> 
    {
        public static ReadOnlyMemoryComparer Default { get; } = new ReadOnlyMemoryComparer(false);
        public static ReadOnlyMemoryComparer Reverse { get; } = new ReadOnlyMemoryComparer(true);

        private readonly bool reverse;

        private ReadOnlyMemoryComparer(bool reverse = false)
        {
            this.reverse = reverse;
        }

        public int Compare(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
        {
            var compare = x.Span.SequenceCompareTo(y.Span);
            return reverse ? -compare : compare;
        }

        public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
        {
            return x.Span.SequenceEqual(y.Span);
        }

        public int GetHashCode(ReadOnlyMemory<byte> obj)
        {
            int hash = 0;
            for (int i = 0; i < obj.Length; i++)
            {
                hash = HashCode.Combine(hash, i, obj.Span[i]);
            }
            return hash;
        }
    }
}
