using System;
using Neo.BlockchainToolkit.Persistence;
using RocksDbSharp;

namespace test.bctklib;

using static Utility;

public class RocksDbFixture : IDisposable
{
    CleanupPath dbPath = new();

    public string DbPath => dbPath;

    public RocksDbFixture()
    {
        using var db = RocksDbUtility.OpenDb(dbPath);
        Populate(db);
    }

    public void Dispose()
    {
        dbPath.Dispose();
        GC.SuppressFinalize(this);
    }

    public static void Populate(RocksDb db)
    {
        var cf = db.GetDefaultColumnFamily();
        foreach (var (key, value) in TestData)
        {
            db.Put(key, value, cf);
        }
    }
}
