using System;
using Neo.BlockchainToolkit.Models;
using Neo.Plugins;

namespace Neo.BlockchainToolkit.Persistence
{
    public interface IRocksDbStorageProvider : IStorageProvider, IDisposable
    {
        void CreateCheckpoint(string checkPointFileName, uint network, byte addressVersion, UInt160 scriptHash);
    }
}