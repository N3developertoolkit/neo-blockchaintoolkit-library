using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.Utilities;
using Neo.Cryptography.MPTTrie;
using Neo.IO;
using Neo.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace test.bctklib;

static class Utility
{
    public static byte[] Bytes(int value) => BitConverter.GetBytes(value);
    public static byte[] Bytes(string value) => System.Text.Encoding.UTF8.GetBytes(value);

    public class CleanupPath : IDisposable
    {
        public readonly string Path;

        public CleanupPath()
        {
            Path = RocksDbUtility.GetTempPath();
        }

        public static implicit operator string(CleanupPath @this) => @this.Path;

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
            GC.SuppressFinalize(this);
        }
    }

    public static Stream GetResourceStream(string name)
    {
        var assembly = typeof(DebugInfoTest).Assembly;
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException();
        return assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException();
    }

    public static JToken GetResourceJson(string name)
    {
        using var resource = GetResourceStream(name);
        using var streamReader = new System.IO.StreamReader(resource);
        using var jsonReader = new JsonTextReader(streamReader);
        return JToken.ReadFrom(jsonReader);
    }

    public static string GetResource(string name)
    {
        using var resource = GetResourceStream(name);
        using var streamReader = new System.IO.StreamReader(resource);
        return streamReader.ReadToEnd();
    }

    public static readonly IReadOnlyList<string> GreekLetters = new[]
    {
        "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta", "Iota", "Kappa",
        "Lambda", "Mu", "Nu", "Xi", "Omicron", "Pi", "Rho", "Sigma", "Tau", "Upsilon",
        "Phi", "Chi", "Psi", "Omega"
    };

    public class ByteArrayEqualityComparer : IEqualityComparer<byte[]>, IComparer<byte[]>
    {
        public static ByteArrayEqualityComparer Default { get; } = new ByteArrayEqualityComparer();

        private ByteArrayEqualityComparer() { }

        public bool Equals(byte[]? x, byte[]? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;
            if (x.Length != y.Length) return false;
            return x.AsSpan().SequenceEqual(y.AsSpan());
        }

        public int GetHashCode([DisallowNull] byte[] obj)
        {
            HashCode hash = default;
            hash.AddBytes(obj);
            return hash.ToHashCode();
        }

        public int Compare(byte[]? x, byte[]? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return 1;
            if (y is null) return -1;
            return x.AsSpan().SequenceCompareTo(y.AsSpan());
        }
    }

    public static IReadOnlyDictionary<byte[], byte[]> TestData => testData.Value;

    static readonly Lazy<IReadOnlyDictionary<byte[], byte[]>> testData =
         new(() =>
         {
             return new Dictionary<byte[], byte[]>(GetTestData(), ByteArrayEqualityComparer.Default);

             static IEnumerable<KeyValuePair<byte[], byte[]>> GetTestData()
             {
                 for (var i = 0; i < GreekLetters.Count; i++)
                 {
                     var key = Bytes(i + 1);
                     var value = Bytes(GreekLetters[i].ToUpper());
                     yield return KVP(key, value);

                     for (byte j = 0; j < 5; j++)
                     {
                         yield return KVP(key.Append(j), value.Append(j));
                     }

                     key = Bytes((i + 1) * -1);
                     value = Bytes(GreekLetters[i].ToLower());
                     yield return KVP(key, value);

                     for (byte j = 0; j < 5; j++)
                     {
                         yield return KVP(key.Append(j), value.Append(j));
                     }
                 }
             }

             static KeyValuePair<byte[], byte[]> KVP(IEnumerable<byte> key, IEnumerable<byte> value)
             {
                 return KeyValuePair.Create(key.ToArray(), value.ToArray());
             }
         });


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
            throw new ArgumentException("Invalid value", nameof(one));
        if (two.start > 9 || two.end > 9 || two.end < two.start)
            throw new ArgumentException("Invalid value", nameof(two));

        for (var i = one.start; i <= one.end; i++)
        {
            for (var j = two.start; j <= two.end; j++)
            {
                yield return (new[] { i, j }, BitConverter.GetBytes(i * 10 + j));
            }
        }
    }

    public static Trie GetTestTrie(Neo.Persistence.IStore store, uint count = 100)
    {
        using var snapshot = store.GetSnapshot();
        var trie = new Trie(snapshot, null);
        for (var i = 0; i < count; i++)
        {
            var key = BitConverter.GetBytes(i);
            var value = Neo.Utility.StrictUTF8.GetBytes($"{i}");
            trie.Put(key, value);
        }
        trie.Commit();
        snapshot.Commit();
        return trie;
    }

    public static byte[] GetSerializedProof(this Trie trie, byte[] key)
    {
        if (trie.TryGetProof(key, out var proof))
        {
            return SerializeProof(key, proof);
        }
        else
        {
            throw new KeyNotFoundException();
        }
    }

    public static byte[] SerializeProof(byte[] key, HashSet<byte[]> proof)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms, Neo.Utility.StrictUTF8);

        writer.WriteVarBytes(key);
        writer.WriteVarInt(proof.Count);
        foreach (var item in proof)
        {
            writer.WriteVarBytes(item);
        }
        writer.Flush();
        return ms.ToArray();
    }
}
