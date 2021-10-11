using System;
using System.Collections.Generic;

namespace Neo.BlockchainToolkit.Persistence
{
    class ByteArrayComparer : IEqualityComparer<byte[]>, IComparer<byte[]>
    {
        public static ByteArrayComparer Default { get; } = new ByteArrayComparer(false);
        public static ByteArrayComparer Reverse { get; } = new ByteArrayComparer(true);

        readonly bool reverse;

        ByteArrayComparer(bool reverse = false)
        {
            this.reverse = reverse;
        }

        public int Compare(byte[]? x, byte[]? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return reverse ? 1 : -1;
            if (y == null) return reverse ? -1 : 1;
            return Compare(x.AsSpan(), y.AsSpan());
        }

        public int Compare(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
        {
            return reverse ? y.SequenceCompareTo(x) : x.SequenceCompareTo(y);
        }

        public bool Equals(byte[]? x, byte[]? y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;
            return x.AsSpan().SequenceEqual(y.AsSpan());
        }

        public int GetHashCode(byte[] obj)
        {
            var hash = new HashCode();
            for (int i = 0; i < obj.Length; i++)
            {
                hash.Add(obj[i]);
            }
            return hash.ToHashCode();
        }
    }
}
