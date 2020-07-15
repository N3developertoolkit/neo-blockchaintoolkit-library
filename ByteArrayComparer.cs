using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Neo.Seattle.Persistence
{
    internal class ByteArrayComparer : IEqualityComparer<byte[]>, IComparer<byte[]>
    {
        private static readonly Lazy<ByteArrayComparer> @default = new Lazy<ByteArrayComparer>(() => new ByteArrayComparer());
        public static ByteArrayComparer Default => @default.Value;

        public int Compare([AllowNull] byte[] x, [AllowNull] byte[] y)
        {
            if (x == null && y == null)
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;
            return x.AsSpan().SequenceCompareTo(y.AsSpan());
        }

        public bool Equals([AllowNull] byte[] x, [AllowNull] byte[] y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;
            return x.AsSpan().SequenceEqual(y.AsSpan());
        }

        public int GetHashCode([DisallowNull] byte[] obj)
        {
            int hash = 0;
            for (int i = 0; i < obj.Length; i++)
            {
                hash = HashCode.Combine(hash, i, obj[i]);
            }
            return hash;
        }
    }
}
