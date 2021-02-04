using System;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public interface IExpressStore : IExpressReadOnlyStore, IStore
    {
        void Put(byte table, byte[]? key, byte[] value);
        void PutSync(byte table, byte[]? key, byte[] value);
        void Delete(byte table, byte[]? key);
    }
}