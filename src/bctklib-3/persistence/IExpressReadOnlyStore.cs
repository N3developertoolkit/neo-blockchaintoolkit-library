using System.Collections.Generic;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public interface IExpressReadOnlyStore
    {
        byte[]? TryGet(byte table, byte[]? key);
        bool Contains(byte table, byte[]? key);
        IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[]? key, SeekDirection direction);
    }
}