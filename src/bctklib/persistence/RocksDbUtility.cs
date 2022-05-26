using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Neo.Wallets;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence;

public static partial class RocksDbUtility
{
    internal static readonly ReadOptions DefaultReadOptions = new();
    internal static readonly WriteOptions WriteSyncOptions = new WriteOptions().SetSync(true);

    public static PinnableSlice GetSlice(this RocksDb db, byte[]? key, ReadOptions? readOptions = null)
        => db.GetSlice(key, null, readOptions);

    public unsafe static PinnableSlice GetSlice(this RocksDb db, ReadOnlySpan<byte> key, ColumnFamilyHandle? columnFamily, ReadOptions? readOptions = null)
    {
        readOptions ??= DefaultReadOptions;

        fixed (byte* keyPtr = key)
        {
            var slice = columnFamily is null
                ? Native.Instance.rocksdb_get_pinned(db.Handle, readOptions.Handle, (IntPtr)keyPtr, (UIntPtr)key.Length)
                : Native.Instance.rocksdb_get_pinned_cf(db.Handle, readOptions.Handle, columnFamily.Handle, (IntPtr)keyPtr, (UIntPtr)key.Length);
            return new PinnableSlice(slice);
        }
    }

    public unsafe static ReadOnlySpan<byte> GetValueSpan(this Iterator iterator)
    {
        IntPtr valuePtr = Native.Instance.rocksdb_iter_value(iterator.Handle, out UIntPtr valueLength);
        return new ReadOnlySpan<byte>((byte*)valuePtr, (int)valueLength);
    }

    public unsafe static void PutV(this WriteBatch writeBatch, ReadOnlySpan<ReadOnlyMemory<byte>> keys, ReadOnlySpan<ReadOnlyMemory<byte>> values, ColumnFamilyHandle? columnFamily)
    {
        var memoryHandles = new List<MemoryHandle>(keys.Length + values.Length);
        try
        {
            var keysList = stackalloc byte*[keys.Length];
            var keysListSizes = stackalloc int[keys.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                var handle = keys[i].Pin();
                memoryHandles.Add(handle);
                keysList[i] = (byte*)handle.Pointer;
                keysListSizes[i] = keys[i].Length;
            }

            var valuesList = stackalloc byte*[values.Length];
            var valuesListSizes = stackalloc int[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var handle = values[i].Pin();
                memoryHandles.Add(handle);
                keysList[i] = (byte*)handle.Pointer;
                keysListSizes[i] = values[i].Length;
            }

            if (columnFamily is null)
            {
                writeBatch.Putv(keys.Length, (IntPtr)keysList, (IntPtr)keysListSizes, values.Length, (IntPtr)valuesList, (IntPtr)valuesListSizes);
            }
            else
            {
                writeBatch.PutvCf(columnFamily.Handle, keys.Length, (IntPtr)keysList, (IntPtr)keysListSizes, values.Length, (IntPtr)valuesList, (IntPtr)valuesListSizes);
            }
        }
        finally
        {
            for (int i = 0; i < memoryHandles.Count; i++)
            {
                memoryHandles[i].Dispose();
            }
        }
    }

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

    public static ColumnFamilyHandle GetColumnFamilyHandle(this RocksDb db, string? columnFamilyName)
    {
        return string.IsNullOrEmpty(columnFamilyName)
            ? db.GetDefaultColumnFamily() 
            : db.GetColumnFamily(columnFamilyName);
    }

    private const string ADDRESS_FILENAME = "ADDRESS" + Constants.EXPRESS_EXTENSION;

    private static string GetAddressFilePath(string directory) => Path.Combine(directory, ADDRESS_FILENAME);

    public static string GetTempPath()
    {
        string tempPath;
        do
        {
            tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }
        while (Directory.Exists(tempPath));
        return tempPath;
    }

    public static void CreateCheckpoint(RocksDb db, string checkPointFileName, uint network, byte addressVersion, UInt160 scriptHash)
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
                writer.WriteLine(network);
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

    public static (uint network, byte addressVersion, UInt160 scriptHash) RestoreCheckpoint(string checkPointArchive, string restorePath,
        uint? network = null, byte? addressVersion = null, UInt160? scriptHash = null)
    {
        var metadata = GetCheckpointMetadata(checkPointArchive);
        if (network.HasValue && network.Value != metadata.network)
            throw new Exception($"checkpoint network ({metadata.network}) doesn't match ({network.Value})");
        if (addressVersion.HasValue && addressVersion.Value != metadata.addressVersion)
            throw new Exception($"checkpoint address version ({metadata.addressVersion}) doesn't match ({addressVersion.Value})");
        if (scriptHash != null && scriptHash != metadata.scriptHash)
            throw new Exception($"checkpoint script hash ({metadata.scriptHash}) doesn't match ({scriptHash})");
        ExtractCheckpoint(checkPointArchive, restorePath);
        return metadata;


        static (uint network, byte addressVersion, UInt160 scriptHash) GetCheckpointMetadata(string checkPointArchive)
        {
            using var archive = ZipFile.OpenRead(checkPointArchive);
            var addressEntry = archive.GetEntry(ADDRESS_FILENAME) ?? throw new InvalidOperationException("Checkpoint missing " + ADDRESS_FILENAME + " file");
            using var addressStream = addressEntry.Open();
            using var addressReader = new StreamReader(addressStream);
            var network = uint.Parse(addressReader.ReadLine() ?? string.Empty);
            var addressVersion = byte.Parse(addressReader.ReadLine() ?? string.Empty);
            var scriptHash = (addressReader.ReadLine() ?? string.Empty).ToScriptHash(addressVersion);

            return (network, addressVersion, scriptHash);
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