using Neo.Plugins.Storage;

namespace Neo.BlockchainToolkit.Persistence
{
    public static class RocksDbStoreExtensions
    {
        public static void CreateCheckpoint(this RocksDbStore store, string checkPointFileName, long magic, string scriptHash)
        {
            CheckpointStore.CreateCheckpoint(store, checkPointFileName, magic, scriptHash);
        }
    }
}
