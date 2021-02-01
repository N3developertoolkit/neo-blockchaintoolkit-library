using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Neo.IO.Caching;
using Neo.Persistence;
using OneOf;

namespace Neo.BlockchainToolkit.Persistence
{
    using RocksDbStore = Neo.Plugins.Storage.RocksDbStore;
    using TrackingMap = ImmutableSortedDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    public partial class CheckpointStore : IStore
    {
        readonly static OneOf.Types.None NONE_INSTANCE = new OneOf.Types.None();
        readonly static TrackingMap EMPTY_TRACKING_MAP = TrackingMap.Empty.WithComparers(ByteArrayComparer.Default);

        readonly IReadOnlyStore store;
        readonly bool disposeStore;
        readonly IDisposable? checkpointCleanup;
        TrackingMap trackingMap = EMPTY_TRACKING_MAP;

        public CheckpointStore(IReadOnlyStore store) : this(store, true, null)
        {
        }

        public CheckpointStore(IReadOnlyStore store, bool disposeStore) : this(store, disposeStore, null)
        {
        }

        public CheckpointStore(IReadOnlyStore store, IDisposable? checkpointCleanup) : this(store, true, checkpointCleanup)
        {
        }

        public CheckpointStore(IReadOnlyStore store, bool disposeStore, IDisposable? checkpointCleanup)
        {
            this.store = store;
            this.disposeStore = disposeStore;
            this.checkpointCleanup = checkpointCleanup;
        }

        private const string ADDRESS_FILENAME = "ADDRESS.neo-express";

        private static string GetAddressFilePath(string directory) => Path.Combine(directory, ADDRESS_FILENAME);

        private static string GetTempPath()
        {
            string tempPath;
            do
            {
                tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(tempPath));
            return tempPath;
        }

        public static void CreateCheckpoint(RocksDbStore store, string checkPointFileName, long magic, string scriptHash)
        {
            if (File.Exists(checkPointFileName))
            {
                throw new ArgumentException("checkpoint file already exists", nameof(checkPointFileName));
            }

            var tempPath = GetTempPath();
            try
            {
                {
                    using var checkpoint = store.Checkpoint();
                    checkpoint.Save(tempPath);
                }

                {
                    using var stream = File.OpenWrite(GetAddressFilePath(tempPath));
                    using var writer = new StreamWriter(stream);
                    writer.WriteLine(magic);
                    writer.WriteLine(scriptHash);
                }

                ZipFile.CreateFromDirectory(tempPath, checkPointFileName);
            }
            finally
            {
                Directory.Delete(tempPath, true);
            }
        }

        public static long RestoreCheckpoint(string checkPointArchive, string restorePath, long magic, string scriptHash)
        {
            var (cpMagic, cpScriptHash) = GetCheckpointMetadata(checkPointArchive);
            if (magic != cpMagic || scriptHash != cpScriptHash)
            {
                throw new Exception("Invalid Checkpoint");
            }

            ExtractCheckpoint(checkPointArchive, restorePath);
            return cpMagic;
        }

        public static long RestoreCheckpoint(string checkPointArchive, string restorePath)
        {
            var (cpMagic, _) = GetCheckpointMetadata(checkPointArchive);
            ExtractCheckpoint(checkPointArchive, restorePath);
            return cpMagic;
        }

        private static (long magic, string scriptHash) GetCheckpointMetadata(string checkPointArchive)
        {
            using var archive = ZipFile.OpenRead(checkPointArchive);
            var addressEntry = archive.GetEntry(ADDRESS_FILENAME) ?? throw new Exception($"Checkpoint archive missing {ADDRESS_FILENAME}");
            using var addressStream = addressEntry.Open();
            using var addressReader = new StreamReader(addressStream);
            var magic = long.Parse(addressReader.ReadLine() ?? string.Empty);
            var scriptHash = addressReader.ReadLine() ?? string.Empty;

            return (magic, scriptHash);
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

        public void Dispose()
        {
            if (disposeStore && store is IDisposable disposable) disposable.Dispose();
            checkpointCleanup?.Dispose();
        }

        byte[]? IReadOnlyStore.TryGet(byte[]? key)
        {
            return TryGet(trackingMap, key);
        }

        byte[]? TryGet(TrackingMap trackingMap, byte[]? key)
        {
            if (trackingMap.TryGetValue(key ?? Array.Empty<byte>(), out var mapValue))
            {
                return mapValue.Match<byte[]?>(v => v, n => null);
            }

            return store.TryGet(key);
        }

        bool IReadOnlyStore.Contains(byte[]? key)
        {
            return Contains(trackingMap, key);
        }

        bool Contains(TrackingMap trackingMap, byte[]? key)
        {
            if (trackingMap.TryGetValue(key ?? Array.Empty<byte>(), out var mapValue))
            {
                return mapValue.Match(v => true, n => false);
            }

            return store.Contains(key);
        }

        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte[]? keyOrPrefix, SeekDirection direction)
        {
            return Seek(trackingMap, keyOrPrefix, direction);
        }

        IEnumerable<(byte[] Key, byte[] Value)> Seek(TrackingMap trackingMap, byte[]? keyOrPrefix, SeekDirection direction)
        {
            keyOrPrefix ??= Array.Empty<byte>();
            var comparer = direction == SeekDirection.Forward ? ByteArrayComparer.Default : ByteArrayComparer.Reverse;

            var memoryItems = trackingMap
                .Where(kvp => kvp.Value.IsT0)
                .Where(kvp => keyOrPrefix.Length == 0 || comparer.Compare(kvp.Key, keyOrPrefix) >= 0)
                .Select(kvp => (kvp.Key, Value: kvp.Value.AsT0));

            var storeItems = store.Seek(keyOrPrefix, direction)
                .Where(kvp => !trackingMap.ContainsKey(kvp.Key));

            return memoryItems.Concat(storeItems).OrderBy(kvp => kvp.Key, comparer);
        }

        void IStore.Put(byte[]? key, byte[] value)
        {
            trackingMap = trackingMap.SetItem(key ?? Array.Empty<byte>(), value);
        }

        void IStore.Delete(byte[]? key)
        {
            trackingMap = trackingMap.SetItem(key ?? Array.Empty<byte>(), NONE_INSTANCE);
        }

        ISnapshot IStore.GetSnapshot()
        {
            throw new NotImplementedException();
        }
    }
}
