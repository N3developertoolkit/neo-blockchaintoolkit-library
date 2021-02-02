using System;

namespace Neo.BlockchainToolkit.Persistence
{
    public interface IExpressStore : IDisposable, IExpressReadOnlyStore
    {
        void Put(byte table, byte[]? key, byte[] value);
        void PutSync(byte table, byte[]? key, byte[] value);
        void Delete(byte table, byte[]? key);
    }
}