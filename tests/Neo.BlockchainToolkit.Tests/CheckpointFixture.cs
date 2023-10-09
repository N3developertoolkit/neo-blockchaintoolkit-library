// Copyright (C) 2023 neo-project
//
// neo-blockchaintoolkit-library is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.BlockchainToolkit.Persistence;
using Neo.Cryptography;
using System;

namespace Neo.BlockchainToolkit.Tests;

using static Utility;

public class CheckpointFixture : IDisposable
{
    readonly CleanupPath checkpointPath = new();
    public string CheckpointPath => checkpointPath;
    public readonly uint Network = ProtocolSettings.Default.Network;
    public readonly byte AddressVersion = ProtocolSettings.Default.AddressVersion;
    public readonly UInt160 ScriptHash = new UInt160(Crypto.Hash160(Bytes("sample-script-hash")));

    public CheckpointFixture()
    {
        using var dbPath = new CleanupPath();
        using var db = RocksDbUtility.OpenDb(dbPath);
        RocksDbFixture.Populate(db);
        RocksDbUtility.CreateCheckpoint(db, CheckpointPath, Network, AddressVersion, ScriptHash);
    }

    public void Dispose()
    {
        checkpointPath.Dispose();
        GC.SuppressFinalize(this);
    }
}
