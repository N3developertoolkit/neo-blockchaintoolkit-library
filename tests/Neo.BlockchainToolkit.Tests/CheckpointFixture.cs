using System;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.Cryptography;

namespace test.bctklib;

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
