using System;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public interface ICheckpoint : IReadOnlyStore
    {
        ProtocolSettings Settings { get; }
    }
}