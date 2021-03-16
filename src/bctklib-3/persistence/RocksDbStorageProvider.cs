using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using Neo.BlockchainToolkit.Models;
using Neo.Persistence;
using Neo.Wallets;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class RocksDbStorageProvider : IDisposableStorageProvider
    {
        static readonly ColumnFamilyOptions defaultColumnFamilyOptions = new ColumnFamilyOptions();

        readonly RocksDb db;
        readonly bool readOnly;

        RocksDbStorageProvider(RocksDb db, ColumnFamilies columnFamilies, bool readOnly)
        {
            this.db = db;
            this.readOnly = readOnly;
        }

        public static RocksDbStorageProvider Open(string path)
        {
            var columnFamilies = GetColumnFamilies(path);
            var db = RocksDb.Open(new DbOptions().SetCreateIfMissing(true), path, columnFamilies);
            return new RocksDbStorageProvider(db, columnFamilies, false);
        }

        public static RocksDbStorageProvider OpenReadOnly(string path)
        {
            var columnFamilies = GetColumnFamilies(path);
            var db = RocksDb.OpenReadOnly(new DbOptions(), path, columnFamilies, false);
            return new RocksDbStorageProvider(db, columnFamilies, true);
        }

        private static ColumnFamilies GetColumnFamilies(string path)
        {
            if (RocksDb.TryListColumnFamilies(new DbOptions(), path, out var names))
            {
                var families = new ColumnFamilies();
                foreach (var name in names)
                {
                    families.Add(name, defaultColumnFamilyOptions);
                }
                return families;
            }

            return new ColumnFamilies();
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public void CreateCheckpoint(string checkPointFileName, ProtocolSettings settings, UInt160 scriptHash)
            => CreateCheckpoint(checkPointFileName, settings.Magic, settings.AddressVersion, scriptHash);

        public void CreateCheckpoint(string checkPointFileName, ExpressChain chain, UInt160 scriptHash)
            => CreateCheckpoint(checkPointFileName, chain.Magic, chain.AddressVersion, scriptHash);

        public void CreateCheckpoint(string checkPointFileName, uint magic, byte addressVersion, UInt160 scriptHash)
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

        public static (uint magic, byte addressVersion) RestoreCheckpoint(string checkPointArchive, string restorePath, ProtocolSettings settings, UInt160 scriptHash)
            => RestoreCheckpoint(checkPointArchive, restorePath, settings.Magic, settings.AddressVersion, scriptHash);

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

        private const string ADDRESS_FILENAME = "ADDRESS" + Constants.EXPRESS_EXTENSION;

        private static string GetAddressFilePath(string directory) => Path.Combine(directory, ADDRESS_FILENAME);

        private static (uint magic, byte addressVersion, UInt160 scriptHash) GetCheckpointMetadata(string checkPointArchive)
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

        private static void ExtractCheckpoint(string checkPointArchive, string restorePath)
        {
            ZipFile.ExtractToDirectory(checkPointArchive, restorePath);
            var addressFile = GetAddressFilePath(restorePath);
            if (File.Exists(addressFile))
            {
                File.Delete(addressFile);
            }
        }

        public IStore GetStore(string path)
        {
            return TryGetStore(path, out var store) ? store : throw new InvalidOperationException("TryGetStore returned false");
        }

        public bool TryGetStore(string path, [NotNullWhen(true)] out IStore? store)
        {
            if (db.TryGetColumnFamily(path, out var columnFamily))
            {
                store = new Store(db, columnFamily, readOnly);
                return true;
            }

            if (!readOnly)
            {
                columnFamily = db.CreateColumnFamily(defaultColumnFamilyOptions, path);
                store = new Store(db, columnFamily, readOnly);
                return true;
            }

            store = null;
            return false;
        }

        private static IEnumerable<(byte[] key, byte[] value)> Seek(RocksDb db, ColumnFamilyHandle columnFamily, byte[]? key, SeekDirection direction, ReadOptions? readOptions)
        {
            key ??= Array.Empty<byte>();
            using var iterator = db.NewIterator(columnFamily, readOptions);

            Func<Iterator> iteratorNext;
            if (direction == SeekDirection.Forward)
            {
                iterator.Seek(key);
                iteratorNext = iterator.Next;
            }
            else
            {
                iterator.SeekForPrev(key);
                iteratorNext = iterator.Prev;
            }

            while (iterator.Valid())
            {
                yield return (iterator.Key(), iterator.Value());
                iteratorNext();
            }
        }
    }
}