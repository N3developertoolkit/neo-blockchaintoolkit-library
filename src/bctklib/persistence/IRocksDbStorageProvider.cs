using System;
using Neo.BlockchainToolkit.Models;
using Neo.Plugins;

namespace Neo.BlockchainToolkit.Persistence
{
    public interface IRocksDbStorageProvider : IStorageProvider, IDisposable
    {
        void CreateCheckpoint(string checkPointFileName, uint network, byte addressVersion, UInt160 scriptHash);

        public void CreateCheckpoint(string checkPointFileName, ProtocolSettings settings, UInt160 scriptHash)
            => CreateCheckpoint(checkPointFileName, settings.Network, settings.AddressVersion, scriptHash);

        public void CreateCheckpoint(string checkPointFileName, ExpressChain chain, UInt160 scriptHash)
            => CreateCheckpoint(checkPointFileName, chain.Network, chain.AddressVersion, scriptHash);
    }
}