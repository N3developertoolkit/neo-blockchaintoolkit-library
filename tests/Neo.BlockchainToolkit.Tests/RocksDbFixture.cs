// Copyright (C) 2023 neo-project
//
// neo-blockchaintoolkit-library is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.BlockchainToolkit.Persistence;
using RocksDbSharp;
using System;

namespace Neo.BlockchainToolkit.Tests;

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
