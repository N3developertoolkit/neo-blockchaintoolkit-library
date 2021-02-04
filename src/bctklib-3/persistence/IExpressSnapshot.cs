using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public interface IExpressSnapshot : IExpressReadOnlyStore, ISnapshot
    {
        void Put(byte table, byte[]? key, byte[] value);
        void Delete(byte table, byte[]? key);
    }
}