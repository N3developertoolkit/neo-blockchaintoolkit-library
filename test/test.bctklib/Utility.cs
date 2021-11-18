using System;
using System.Collections.Generic;
using System.IO;
using Neo.Persistence;
using Nito.Disposables;

namespace test.bctklib3
{
    static class Utility
    {
        public static IDisposable GetDeleteDirectoryDisposable(string path)
        {
            return AnonymousDisposable.Create(() =>
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            });
        }

        public static void PutSeekData(this IStore store, (byte start, byte end) one, (byte start, byte end) two)
        {
            foreach (var (key, value) in GetSeekData(one, two))
            {
                store.Put(key, value);
            }
        }

        public static IEnumerable<(byte[], byte[])> GetSeekData((byte start, byte end) one, (byte start, byte end) two)
        {
            if (one.start > 9 || one.end > 9 || one.end < one.start)
                throw new ArgumentException(nameof(one));
            if (two.start > 9 || two.end > 9 || two.end < two.start)
                throw new ArgumentException(nameof(two));

            for (var i = one.start; i <= one.end; i++)
            {
                for (var j = two.start; j <= two.end; j++)
                {
                    yield return (new[] { i, j }, BitConverter.GetBytes(i * 10 + j));
                }
            }
        }
    }
}
