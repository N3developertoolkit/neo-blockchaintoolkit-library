namespace Neo.BlockchainToolkit.Persistence
{
    public interface IExpressSnapshot : IExpressReadOnlyStore
    {
        void Commit();
        void Put(byte table, byte[]? key, byte[] value);
        void Delete(byte table, byte[]? key);
    }
}