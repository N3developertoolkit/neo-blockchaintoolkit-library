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
            return Equals(x.Span, y.Span);
        }

        public int GetHashCode(ReadOnlyMemory<byte> obj)
        {
            return GetHashCode(obj.Span);
        }

        public static bool Equals(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
        {
            return x.SequenceEqual(y);
        }

        public static int GetHashCode(ReadOnlySpan<byte> span)
        {
            var hash = default(HashCode);
            for (int i = 0; i < span.Length; i++)
            {
                hash.Add(i);
                hash.Add(span[i]);
            }
            return hash.ToHashCode();
        }
    }
}
