using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Neo.Persistence;
using Neo.Wallets;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class RocksDbStore : IStore, IExpressStore
    {
        private readonly bool readOnly;
        private readonly RocksDb db;
        private readonly ConcurrentDictionary<byte, ColumnFamilyHandle> columnFamilyCache;
        private readonly ReadOptions readOptions = new ReadOptions();
        private readonly WriteOptions writeOptions = new WriteOptions();
        private readonly WriteOptions writeSyncOptions = new WriteOptions().SetSync(true);
        private static readonly ColumnFamilyOptions defaultColumnFamilyOptions = new ColumnFamilyOptions();

        public static RocksDbStore Open(string path)
        {
            var columnFamilies = GetColumnFamilies(path);
            var db = RocksDb.Open(new DbOptions().SetCreateIfMissing(true), path, columnFamilies);
            return new RocksDbStore(db, columnFamilies);
        }

        public static RocksDbStore OpenReadOnly(string path)
        {
            var columnFamilies = GetColumnFamilies(path);
            var db = RocksDb.OpenReadOnly(new DbOptions(), path, columnFamilies, false);
            return new RocksDbStore(db, columnFamilies, true);
        }

        private RocksDbStore(RocksDb db, ColumnFamilies columnFamilies, bool readOnly = false)
        {
            this.readOnly = readOnly;
            this.db = db;
            this.columnFamilyCache = new ConcurrentDictionary<byte, ColumnFamilyHandle>(EnumerateColumnFamlies(db, columnFamilies));

            static IEnumerable<KeyValuePair<byte, ColumnFamilyHandle>> EnumerateColumnFamlies(RocksDb db, ColumnFamilies columnFamilies)
            {
                foreach (var descriptor in columnFamilies)
                {
                    var name = descriptor.Name;
                    if (byte.TryParse(descriptor.Name, out var key))
                    {
                        var value = db.GetColumnFamily(name);
                        yield return KeyValuePair.Create(key, value);
                    }
                }
            }
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public void CreateCheckpoint(string checkPointFileName, ProtocolSettings settings, UInt160 scriptHash)
            => CreateCheckpoint(checkPointFileName, settings.Magic, settings.AddressVersion, scriptHash);

        public void CreateCheckpoint(string checkPointFileName, uint magic, byte addressVersion, UInt160 scriptHash)
        {
            if (File.Exists(checkPointFileName))
            {
                throw new ArgumentException("checkpoint file already exists", nameof(checkPointFileName));
            }

            var tempPath = GetTempPath();
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

            static string GetTempPath()
            {
                string tempPath;
                do
                {
                    tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                }
                while (Directory.Exists(tempPath));
                return tempPath;
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

        private const string ADDRESS_FILENAME = "ADDRESS.neo-express";

        private static string GetAddressFilePath(string directory) =>
            Path.Combine(directory, ADDRESS_FILENAME);

        private static (uint magic, byte addressVersion, UInt160 scriptHash) GetCheckpointMetadata(string checkPointArchive)
        {
            using var archive = ZipFile.OpenRead(checkPointArchive);
            var addressEntry = archive.GetEntry(ADDRESS_FILENAME) ?? throw new Exception();
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

        public byte[]? TryGet(byte table, byte[]? key)
            => db.Get(key ?? Array.Empty<byte>(), GetColumnFamily(table), readOptions);

        public bool Contains(byte table, byte[]? key) => TryGet(table, key) != null;

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[]? key, SeekDirection direction)
            => Seek(db, key, GetColumnFamily(table), direction, readOptions);

        public void Put(byte table, byte[]? key, byte[] value)
        {
            if (readOnly) throw new InvalidOperationException();
            db.Put(key ?? Array.Empty<byte>(), value, GetColumnFamily(table), writeOptions);
        }

        public void PutSync(byte table, byte[]? key, byte[] value)
        {
            if (readOnly) throw new InvalidOperationException();
            db.Put(key ?? Array.Empty<byte>(), value, GetColumnFamily(table), writeSyncOptions);
        }

        public void Delete(byte table, byte[]? key)
        {
            if (readOnly) throw new InvalidOperationException();
            db.Remove(key ?? Array.Empty<byte>(), GetColumnFamily(table), writeOptions);
        }

        byte[]? IReadOnlyStore.TryGet(byte[]? key) => TryGet(default, key);
        bool IReadOnlyStore.Contains(byte[] key) => Contains(default, key);
        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte[]? key, SeekDirection direction) => Seek(default, key, direction);
        void IStore.Put(byte[]? key, byte[] value) => Put(default, key, value);
        void IStore.PutSync(byte[]? key, byte[] value) => PutSync(default, key, value);
        void IStore.Delete(byte[]? key) => Delete(default, key);
        ISnapshot IStore.GetSnapshot() => readOnly ? throw new InvalidOperationException() : new Snapshot(this);

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

        private ColumnFamilyHandle GetColumnFamily(byte table)
        {
            return columnFamilyCache.GetOrAdd(table, t => GetColumnFamilyFromDatabase(db, t));

            static ColumnFamilyHandle GetColumnFamilyFromDatabase(RocksDb db, byte table)
            {
                var familyName = table.ToString();
                if (db.TryGetColumnFamily(familyName, out var columnFamily))
                {
                    return columnFamily;
                }

                return db.CreateColumnFamily(defaultColumnFamilyOptions, familyName);
            }
        }

        private static IEnumerable<(byte[] key, byte[] value)> Seek(RocksDb db, byte[]? prefix, ColumnFamilyHandle columnFamily, SeekDirection direction, ReadOptions? readOptions)
        {
            prefix ??= Array.Empty<byte>();
            using var iterator = db.NewIterator(columnFamily, readOptions);

            Func<Iterator> iteratorNext;
            if (direction == SeekDirection.Forward)
            {
                iterator.Seek(prefix);
                iteratorNext = iterator.Next;
            }
            else
            {
                iterator.SeekForPrev(prefix);
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