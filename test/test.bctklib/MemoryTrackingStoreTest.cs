using System;
using System.Collections.Generic;
using System.Linq;
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

        static IStore GetSeekStore()
        {
            var memoryStore = new MemoryStore();
            memoryStore.Put(new byte[] { 0x00, 0x01 }, BitConverter.GetBytes(1));
            memoryStore.Put(new byte[] { 0x00, 0x02 }, BitConverter.GetBytes(2));
            memoryStore.Put(new byte[] { 0x01, 0x01 }, BitConverter.GetBytes(11));
            memoryStore.Put(new byte[] { 0x01, 0x02 }, BitConverter.GetBytes(12));
            memoryStore.Put(new byte[] { 0x02, 0x01 }, BitConverter.GetBytes(21));
            memoryStore.Put(new byte[] { 0x02, 0x02 }, BitConverter.GetBytes(22));

            var store = new MemoryTrackingStore(memoryStore);
            store.Put(new byte[] { 0x00, 0x03 }, BitConverter.GetBytes(3));
            store.Put(new byte[] { 0x00, 0x04 }, BitConverter.GetBytes(4));
            store.Put(new byte[] { 0x01, 0x03 }, BitConverter.GetBytes(13));
            store.Put(new byte[] { 0x01, 0x04 }, BitConverter.GetBytes(14));
            store.Put(new byte[] { 0x02, 0x03 }, BitConverter.GetBytes(23));
            store.Put(new byte[] { 0x02, 0x04 }, BitConverter.GetBytes(24));

            return store;
        }

        static IEnumerable<(byte[], byte[])> GetSeekExpected()
        {
            for (byte i = 0; i <= 2; i++)
            {
                for (byte j = 1; j <= 4; j++)
                {
                    var k = new byte[] { i, j };
                    var v = i * 10 + j;
                    yield return (k, BitConverter.GetBytes(v));
                }
            }
        }

        [Fact]
        public void can_seek_forward_no_prefix()
        {
            var store = GetSeekStore();

            var actual = store.Seek(Array.Empty<byte>(), SeekDirection.Forward).ToArray();
            var expected = GetSeekExpected().ToArray();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void can_seek_backwards_no_prefix()
        {
            var store = GetSeekStore();

            var actual = store.Seek(Array.Empty<byte>(), SeekDirection.Backward).ToArray();
            var expected = GetSeekExpected().Reverse().ToArray();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void seek_forwards_with_prefix()
        {
            var store = GetSeekStore();

            var actual = store.Seek(new byte[] { 0x01 }, SeekDirection.Forward).ToArray();
            var expected = GetSeekExpected().Where(kvp => kvp.Item1[0] >= 0x01).ToArray();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void seek_backwards_with_prefix()
        {
            var store = GetSeekStore();

            var actual = store.Seek(new byte[] { 0x02 }, SeekDirection.Backward).ToArray();
            var expected = GetSeekExpected().Where(kvp => kvp.Item1[0] <= 0x01).Reverse().ToArray();
            Assert.Equal(expected, actual);
        }
    }
}
