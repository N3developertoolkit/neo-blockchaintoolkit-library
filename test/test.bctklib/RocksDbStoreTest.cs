using System;
using Neo.BlockchainToolkit.Persistence;
using Xunit;

namespace test.bctklib3
{
    public class RocksDbStoreTest
    {
        [Fact]
        public void can_put_and_get()
        {
            var path = Utility.GetTempPath();
            using var _ = Utility.GetDeleteDirectoryDisposable(path);

            {
                var db = RocksDbUtility.OpenDb(path);
                using var store = new RocksDbStore(db, db.GetDefaultColumnFamily(), false, false);
                store.Put(new byte[] { 0x00, 0x03 }, BitConverter.GetBytes(3));
                store.Put(new byte[] { 0x00, 0x04 }, BitConverter.GetBytes(4));
                store.Put(new byte[] { 0x01, 0x03 }, BitConverter.GetBytes(13));
                store.Put(new byte[] { 0x01, 0x04 }, BitConverter.GetBytes(14));
                store.Put(new byte[] { 0x02, 0x03 }, BitConverter.GetBytes(23));
                store.Put(new byte[] { 0x02, 0x04 }, BitConverter.GetBytes(24));
            }

            {
                var db = RocksDbUtility.OpenReadOnlyDb(path);
                using var store = new RocksDbStore(db, db.GetDefaultColumnFamily(), true, false);
                var actual = store.TryGet(new byte[] { 0x00, 0x03 });
                Assert.Equal<byte[]>(BitConverter.GetBytes(3), actual!);
            }
        }

        [Fact]
        public void readonly_store_throws_on_put_and_delete()
        {
            var path = Utility.GetTempPath();
            using var _ = Utility.GetDeleteDirectoryDisposable(path);

            {
                var db = RocksDbUtility.OpenDb(path);
                using var store = new RocksDbStore(db, db.GetDefaultColumnFamily(), false, false);
                store.Put(new byte[] { 0x00, 0x03 }, BitConverter.GetBytes(3));
                store.Put(new byte[] { 0x00, 0x04 }, BitConverter.GetBytes(4));
                store.Put(new byte[] { 0x01, 0x03 }, BitConverter.GetBytes(13));
                store.Put(new byte[] { 0x01, 0x04 }, BitConverter.GetBytes(14));
                store.Put(new byte[] { 0x02, 0x03 }, BitConverter.GetBytes(23));
                store.Put(new byte[] { 0x02, 0x04 }, BitConverter.GetBytes(24));
            }

            {
                var db = RocksDbUtility.OpenReadOnlyDb(path);
                using var store = new RocksDbStore(db, db.GetDefaultColumnFamily(), true, false);

                Assert.Throws<InvalidOperationException>(() => store.Put(new byte[] { 0x04, 0x03 }, BitConverter.GetBytes(43)));
                Assert.Throws<InvalidOperationException>(() => store.Delete(new byte[] { 0x00, 0x03 }));
            }
        }
    }
}
