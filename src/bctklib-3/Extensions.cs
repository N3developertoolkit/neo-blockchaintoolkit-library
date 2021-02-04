
using System;
using System.IO.Abstractions;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;

namespace Neo.BlockchainToolkit
{
    public static class Extensions
    {
        public static ExpressChain LoadChain(this IFileSystem fileSystem, string path)
        {
            var serializer = new JsonSerializer();
            using var stream = fileSystem.File.OpenRead(path);
            using var streamReader = new System.IO.StreamReader(stream);
            using var reader = new JsonTextReader(streamReader);
            return serializer.Deserialize<ExpressChain>(reader)
                ?? throw new Exception($"Cannot load Neo-Express instance information from {path}");
        }

        public static void SaveChain(this IFileSystem fileSystem, ExpressChain chain, string path)
        {
            var serializer = new JsonSerializer();
            using var stream = fileSystem.File.Open(path, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            using var streamWriter = new System.IO.StreamWriter(stream);
            using var writer = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented };
            serializer.Serialize(writer, chain);
        }
    }
}
