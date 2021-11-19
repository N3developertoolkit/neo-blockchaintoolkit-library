using System;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public interface ICheckpointStore : IReadOnlyStore
    {
        ProtocolSettings Settings { get; }
    }
}