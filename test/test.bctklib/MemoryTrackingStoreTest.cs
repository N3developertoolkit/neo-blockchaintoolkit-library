using System;
using System.Text;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using Xunit;

namespace test.bctklib3
{
    public class MemoryTrackingStoreTest
    {
        static readonly byte[] helloValue = Encoding.UTF8.GetBytes("Hello");
        static readonly byte[] worldValue = Encoding.UTF8.GetBytes("World");

        [Fact]
        public void can_get_value_from_underlying_store()
        {
            var key = new byte[] { 0x00 };
            var memoryStore = new MemoryStore();
            memoryStore.Put(key, helloValue);

            var trackingStore = new MemoryTrackingStore(memoryStore);
            var actual = trackingStore.TryGet(key);

            Assert.Equal(helloValue, actual);
        }

        [Fact]
        public void can_overwrite_existing_value()
        {
            var key = new byte[] { 0x00 };
            var memoryStore = new MemoryStore();
            memoryStore.Put(key, helloValue);

            var trackingStore = new MemoryTrackingStore(memoryStore);
            trackingStore.Put(key, worldValue);
            var actual = trackingStore.TryGet(key);

            Assert.Equal(worldValue, actual);
        }

        [Fact]
        public void can_delete_existing_value()
        {
            var key = new byte[] { 0x00 };
            var memoryStore = new MemoryStore();
            memoryStore.Put(key, helloValue);

            var trackingStore = new MemoryTrackingStore(memoryStore);
            trackingStore.Delete(key);
            var actual = trackingStore.TryGet(key);

            Assert.Null(actual);
        }

        [Fact]
        public void can_put_new_value()
        {
            var key = new byte[] { 0x00 };
            var store = new MemoryTrackingStore(NullStore.Instance);
            store.Put(key, helloValue);
            var actual = store.TryGet(key);
            Assert.Equal(helloValue, actual);
        }

        [Fact]
        public void snapshot_doesnt_see_store_changes()
        {
            var store = new MemoryTrackingStore(NullStore.Instance);
            using var snapshot = store.GetSnapshot();

            var key = new byte[] { 0x00 };
            store.Put(key, helloValue);

            Assert.Null(snapshot.TryGet(key));
            Assert.Equal(helloValue, store.TryGet(key));
        }

        [Fact]
        public void can_update_store_via_snapshot()
        {
            var store = new MemoryTrackingStore(NullStore.Instance);
            using var snapshot = store.GetSnapshot();

            var key = new byte[] { 0x00 };
            snapshot.Put(key, helloValue);

            Assert.Null(store.TryGet(key));
            snapshot.Commit();
            Assert.Equal(helloValue, store.TryGet(key));
        }

        [Fact]
        public void can_delete_via_snapshot()
        {
            var store = new MemoryTrackingStore(NullStore.Instance);
            var key = new byte[] { 0x00 };
            store.Put(key, helloValue);

            using var snapshot = store.GetSnapshot();
            snapshot.Delete(key);

            Assert.Equal(helloValue, store.TryGet(key));
            snapshot.Commit();
            Assert.Null(store.TryGet(key));
        }

        [Fact]
        public void update_key_array_doesnt_affect_store()
        {
            var store = new MemoryTrackingStore(NullStore.Instance);
            var key = new byte[] { 0x00 };
            store.Put(key, helloValue);

            key[0] = 0xff;
            Assert.Equal(helloValue, store.TryGet(new byte[] { 0x00 }));
            Assert.Null(store.TryGet(new byte[] { 0xff }));
        }

        [Fact(Skip = "Not Implemented")]
        public void can_seek()
        {
            throw new NotImplementedException();
        }
    }
}
