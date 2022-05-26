using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    // Note, this interface is only used for tests
    internal interface ITrackingStore : IStore
    {
        IReadOnlyStore UnderlyingStore { get; }
    }
}