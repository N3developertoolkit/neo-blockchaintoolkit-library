using System;

namespace Neo.BlockchainToolkit.Persistence
{
    public interface IDisposableStorageProvider : Plugins.IStorageProvider, IDisposable
    {
    }
}