using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using Xunit;
using static System.Text.Encoding;

namespace test.bctklib3
{
    public class MemoryTrackingStoreTest
    {
        static readonly byte[] helloValue = UTF8.GetBytes("Hello");
        static readonly byte[] worldValue = UTF8.GetBytes("World");

        static byte[] Bytes(params byte[] values) => values;
        static byte[] Bytes(int value) => BitConverter.GetBytes(value);

        class NonDisposableStore : IReadOnlyStore
        {
            byte[] IReadOnlyStore.TryGet(byte[] key) => throw new NotImplementedException();
            bool IReadOnlyStore.Contains(byte[] key) => throw new NotImplementedException();
            IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte[] key, SeekDirection direction)
                => throw new NotImplementedException();
        }

        class DisposableStore : IReadOnlyStore, IDisposable
        {
            public bool Disposed { get; private set; } = false;

            public void Dispose()
            {
                Disposed = true;
            }

            byte[] IReadOnlyStore.TryGet(byte[] key) => throw new NotImplementedException();
            bool IReadOnlyStore.Contains(byte[] key) => throw new NotImplementedException();
            IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte[] key, SeekDirection direction)
                => throw new NotImplementedException();
        }

        [Fact]
        public void disposes_underlying_store_if_disposable()
        {
            var underStore = new DisposableStore();
            using (var store = new MemoryTrackingStore(underStore))
            {
            }
            underStore.Disposed.Should().BeTrue();
        }

        
        [Fact]
        public void doesnt_disposes_underlying_store_if_not_disposable()
        {
            var underStore = new NonDisposableStore();
            using (var store = new MemoryTrackingStore(underStore))
            {
            }
        }

        [Fact]
        public void can_get_value_from_underlying_store()
        {
            var key = Bytes(0);
            var memoryStore = new MemoryStore();
            memoryStore.Put(key, helloValue);

            var trackingStore = new MemoryTrackingStore(memoryStore);
            var actual = trackingStore.TryGet(key);

            Assert.Equal(helloValue, actual);
        }

        [Fact]
        public void can_overwrite_existing_value()
        {
            var key = Bytes(0);
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
            var key = Bytes(0);
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
            var key = Bytes(0);
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

            var key = Bytes(0);
            store.Put(key, helloValue);

            Assert.Null(snapshot.TryGet(key));
            Assert.Equal(helloValue, store.TryGet(key));
        }

        [Fact]
        public void can_update_store_via_snapshot()
        {
            var store = new MemoryTrackingStore(NullStore.Instance);
            using var snapshot = store.GetSnapshot();

            var key = Bytes(0);
            snapshot.Put(key, helloValue);

            Assert.Null(store.TryGet(key));
            snapshot.Commit();
            Assert.Equal(helloValue, store.TryGet(key));
        }

        [Fact]
        public void can_delete_via_snapshot()
        {
            var store = new MemoryTrackingStore(NullStore.Instance);
            var key = Bytes(0);
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
            var key = Bytes(0);
            store.Put(key, helloValue);

            key[0] = 0xff;
            Assert.Equal(helloValue, store.TryGet(Bytes(0)));
            Assert.Null(store.TryGet(key));
        }

        static IStore GetSeekStore()
        {
            var memoryStore = new MemoryStore();
            memoryStore.PutSeekData((0,2), (1,2));

            var store = new MemoryTrackingStore(memoryStore);
            store.PutSeekData((0,2), (3,4));

            return store;
        }

        static IEnumerable<(byte[], byte[])> GetSeekData() => Utility.GetSeekData((0,2), (1,4));

        [Fact]
        public void can_seek_forward_no_prefix()
        {
            var store = GetSeekStore();

            var actual = store.Seek(Array.Empty<byte>(), SeekDirection.Forward);
            var expected = GetSeekData();
            actual.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void can_seek_backwards_no_prefix()
        {
            var store = GetSeekStore();

            var actual = store.Seek(Array.Empty<byte>(), SeekDirection.Backward);
            var expected = GetSeekData().Reverse();
            actual.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void seek_forwards_with_prefix()
        {
            var store = GetSeekStore();

            var actual = store.Seek(Bytes(1), SeekDirection.Forward);
            var expected = GetSeekData().Where(kvp => kvp.Item1[0] >= 0x01);
            actual.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void seek_backwards_with_prefix()
        {
            var store = GetSeekStore();

            var actual = store.Seek(Bytes(0x02), SeekDirection.Backward);
            var expected = GetSeekData().Where(kvp => kvp.Item1[0] <= 0x01).Reverse();
            actual.Should().BeEquivalentTo(expected);
        }
    }
}
