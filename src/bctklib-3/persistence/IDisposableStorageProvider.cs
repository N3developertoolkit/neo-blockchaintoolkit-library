using System;
using System.Collections.Immutable;
using OneOf;

namespace Neo.BlockchainToolkit.Persistence
{
    public interface IDisposableStorageProvider : Plugins.IStorageProvider, IDisposable
    {
    }
}