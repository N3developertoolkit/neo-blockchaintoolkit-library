using System;
using System.IO;
using System.IO.Compression;
using Neo.Wallets;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public static class RocksDbUtility
    {
        public static RocksDb OpenDb(string path)
        {
            var columnFamilies = GetColumnFamilies(path);
            return RocksDb.Open(new DbOptions().SetCreateIfMissing(true), path, columnFamilies);
        }

        public static RocksDb OpenReadOnlyDb(string path)
        {
            var columnFamilies = GetColumnFamilies(path);
            return RocksDb.OpenReadOnly(new DbOptions(), path, columnFamilies, false);
        }

        static ColumnFamilies GetColumnFamilies(string path)
        {
            if (RocksDb.TryListColumnFamilies(new DbOptions(), path, out var names))
            {
                var columnFamilyOptions = new ColumnFamilyOptions();
                var families = new ColumnFamilies();
                foreach (var name in names)
                {
                    families.Add(name, columnFamilyOptions);
                }
                return families;
            }

            return new ColumnFamilies();
        }

        private const string ADDRESS_FILENAME = "ADDRESS" + Constants.EXPRESS_EXTENSION;

        private static string GetAddressFilePath(string directory) => Path.Combine(directory, ADDRESS_FILENAME);

        public static void CreateCheckpoint(RocksDb db, string checkPointFileName, uint magic, byte addressVersion, UInt160 scriptHash)
        {
            if (File.Exists(checkPointFileName))
            {
                throw new ArgumentException("checkpoint file already exists", nameof(checkPointFileName));
            }

            string tempPath;
            do
            {
                tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(tempPath));

            try
            {
                {
                    using var checkpoint = db.Checkpoint();
                    checkpoint.Save(tempPath);
                }

                {
                    using var stream = File.OpenWrite(GetAddressFilePath(tempPath));
                    using var writer = new StreamWriter(stream);
                    writer.WriteLine(magic);
                    writer.WriteLine(addressVersion);
                    writer.WriteLine(scriptHash.ToAddress(addressVersion));
                }

                ZipFile.CreateFromDirectory(tempPath, checkPointFileName);
            }
            finally
            {
                Directory.Delete(tempPath, true);
            }
        }

        public static (uint magic, byte addressVersion) RestoreCheckpoint(string checkPointArchive, string restorePath, uint magic, byte addressVersion, UInt160 scriptHash)
        {
            var metadata = GetCheckpointMetadata(checkPointArchive);
            if (magic != metadata.magic
                || addressVersion != metadata.addressVersion
                || scriptHash != metadata.scriptHash)
            {
                throw new Exception("Invalid Checkpoint");
            }

            ExtractCheckpoint(checkPointArchive, restorePath);
            return (metadata.magic, metadata.addressVersion);
        }

        public static (uint magic, byte addressVersion) RestoreCheckpoint(string checkPointArchive, string restorePath)
        {
            var metadata = GetCheckpointMetadata(checkPointArchive);
            ExtractCheckpoint(checkPointArchive, restorePath);
            return (metadata.magic, metadata.addressVersion);
        }

        static (uint magic, byte addressVersion, UInt160 scriptHash) GetCheckpointMetadata(string checkPointArchive)
        {
            using var archive = ZipFile.OpenRead(checkPointArchive);
            var addressEntry = archive.GetEntry(ADDRESS_FILENAME) ?? throw new InvalidOperationException("Checkpoint missing " + ADDRESS_FILENAME + " file");
            using var addressStream = addressEntry.Open();
            using var addressReader = new StreamReader(addressStream);
            var magic = uint.Parse(addressReader.ReadLine() ?? string.Empty);
            var addressVersion = byte.Parse(addressReader.ReadLine() ?? string.Empty);
            var scriptHash = (addressReader.ReadLine() ?? string.Empty).ToScriptHash(addressVersion);

            return (magic, addressVersion, scriptHash);
        }

        static void ExtractCheckpoint(string checkPointArchive, string restorePath)
        {
            ZipFile.ExtractToDirectory(checkPointArchive, restorePath);
            var addressFile = GetAddressFilePath(restorePath);
            if (File.Exists(addressFile))
            {
                File.Delete(addressFile);
            }
        }
    }
}