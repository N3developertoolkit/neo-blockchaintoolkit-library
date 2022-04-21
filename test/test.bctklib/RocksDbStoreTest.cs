using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using RocksDbSharp;
using Xunit;
using static System.Text.Encoding;

namespace test.bctklib
{
    public class RocksDbStoreTest
    {
        static byte[] Bytes(int value) => BitConverter.GetBytes(value);
        static byte[] Bytes(string value) => UTF8.GetBytes(value);

        static void RunTestWithCleanup(Action<string> test)
        {
            var dbPath = RocksDbUtility.GetTempPath();
            try
            {
                test(dbPath);
            }
            finally
            {
                if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true);
            }
        }

        static void Populate(string path, params (byte[] key, byte[] value)[] values)
        {
            using var db = RocksDbUtility.OpenDb(path);
            var cf = db.GetDefaultColumnFamily();
            var writeOptions = new WriteOptions().SetSync(true);
            for (int i = 0; i < values.Length; i++)
            {
                var (key, value) = values[i];
                db.Put(key, value, cf, writeOptions);
            }
        }

        static IStore GetSeekStore(string path)
        {
            Populate(path);
            var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
            store.PutSeekData((0, 2), (1, 4));
            return store;
        }

        static IEnumerable<(byte[], byte[])> GetSeekData() => Utility.GetSeekData((0, 2), (1, 4));

        [Fact]
        public void readonly_store_throws_on_write_operations()
        {
            RunTestWithCleanup(path =>
            {
                Populate(path);

                var key = Bytes(0);
                var hello = Bytes("hello");
                using var store = new RocksDbStore(RocksDbUtility.OpenReadOnlyDb(path), readOnly: true);
                Assert.Throws<InvalidOperationException>(() => store.Put(key, hello));
                Assert.Throws<InvalidOperationException>(() => store.PutSync(key, hello));
                Assert.Throws<InvalidOperationException>(() => store.Delete(key));
                Assert.Throws<InvalidOperationException>(() => store.GetSnapshot());
            });
        }

        [Fact]
        public void store_sharing()
        {
            RunTestWithCleanup(path =>
            {
                var key = Bytes(0);
                var hello = Bytes("hello");

                Populate(path, (key, hello));

                using var db = RocksDbUtility.OpenReadOnlyDb(path);
                var cf = db.GetDefaultColumnFamily();

                using (var store = new RocksDbStore(db, cf, readOnly: true, shared: true))
                {
                    store.TryGet(key).Should().BeEquivalentTo(hello);
                }

                using (var store = new RocksDbStore(db, cf, readOnly: true, shared: false))
                {
                    store.TryGet(key).Should().BeEquivalentTo(hello);
                }

                using (var store = new RocksDbStore(db, cf, readOnly: true, shared: false))
                {
                    Assert.Throws<ObjectDisposedException>(() => store.TryGet(key));
                }
            });
        }

        [Fact]
        public void can_get_value_from_store()
        {
            RunTestWithCleanup(path =>
            {
                var key = Bytes(0);
                var hello = Bytes("hello");

                Populate(path, (key, hello));

                using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
                store.Contains(key).Should().BeTrue();
                store.TryGet(key).Should().BeEquivalentTo(hello);
            });
        }

        [Fact]
        public void contains_false_for_missing_key()
        {
            RunTestWithCleanup(path =>
            {
                Populate(path);

                using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
                store.Contains(Bytes("invalid-key")).Should().BeFalse();
            });
        }

        [Fact]
        public void can_overwrite_existing_value()
        {
            RunTestWithCleanup(path =>
            {
                var key = Bytes(0);
                var hello = Bytes("hello");
                var world = Bytes("world");

                Populate(path, (key, hello));

                using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
                store.Put(key, world);
                store.Contains(key).Should().BeTrue();
                store.TryGet(key).Should().BeEquivalentTo(world);
            });
        }

        [Fact]
        public void can_delete_existing_value()
        {
            RunTestWithCleanup(path =>
            {
                var key = Bytes(0);
                var hello = Bytes("hello");

                Populate(path, (key, hello));

                using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
                store.Contains(key).Should().BeTrue();
                store.Delete(key);
                store.Contains(key).Should().BeFalse();
                store.TryGet(key).Should().BeNull();
            });
        }

        [Fact]
        public void cant_put_null_value()
        {
            RunTestWithCleanup(path =>
            {
                Populate(path);

                using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
                var key = Bytes(0);
                Assert.Throws<NullReferenceException>(() => store.Put(key, null));
            });
        }

        [Fact]
        public void snapshot_doesnt_see_store_changes()
        {
            RunTestWithCleanup(path =>
            {
                var key = Bytes(0);
                var hello = Bytes("hello");

                Populate(path);

                using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
                using var snapshot = store.GetSnapshot();

                store.Put(key, hello);
                snapshot.Contains(key).Should().BeFalse();
                snapshot.TryGet(key).Should().BeNull();
            });
        }

        [Fact]
        public void can_update_store_via_snapshot()
        {
            RunTestWithCleanup(path =>
            {
                var key = Bytes(0);
                var hello = Bytes("hello");

                Populate(path);

                using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
                using var snapshot = store.GetSnapshot();

                snapshot.Put(key, hello);

                store.TryGet(key).Should().BeNull();
                snapshot.Commit();
                store.TryGet(key).Should().BeEquivalentTo(hello);
            });
        }

        [Fact]
        public void cant_put_null_value_to_snapshot()
        {
            RunTestWithCleanup(path =>
            {
                Populate(path);

                using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
                using var snapshot = store.GetSnapshot();

                var key = Bytes(0);
                Assert.Throws<NullReferenceException>(() => snapshot.Put(key, null));
            });
        }


        [Fact]
        public void can_seek_forward_no_prefix()
        {
            RunTestWithCleanup(path =>
            {
                using var store = GetSeekStore(path);

                var actual = store.Seek(Array.Empty<byte>(), SeekDirection.Forward);
                var expected = GetSeekData();
                actual.Should().BeEquivalentTo(expected);
            });
        }

        [Fact]
        public void can_seek_backwards_no_prefix()
        {
            RunTestWithCleanup(path =>
            {
                using var store = GetSeekStore(path);

                var actual = store.Seek(Array.Empty<byte>(), SeekDirection.Backward).ToArray();
                actual.Should().BeEmpty();
            });
        }

        [Fact]
        public void seek_forwards_with_prefix()
        {
            RunTestWithCleanup(path =>
            {
                using var store = GetSeekStore(path);

                var actual = store.Seek(Bytes(1), SeekDirection.Forward);
                var expected = GetSeekData().Where(kvp => kvp.Item1[0] >= 0x01);
                actual.Should().BeEquivalentTo(expected);
            });
        }

        [Fact]
        public void seek_backwards_with_prefix()
        {
            RunTestWithCleanup(path =>
            {
                using var store = GetSeekStore(path);

                var actual = store.Seek(Bytes(2), SeekDirection.Backward);
                var expected = GetSeekData().Where(kvp => kvp.Item1[0] <= 0x01).Reverse();
                actual.Should().BeEquivalentTo(expected);
            });
        }
    }
}
