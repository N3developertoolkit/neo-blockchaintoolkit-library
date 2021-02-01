using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo.BlockchainToolkit.Persistence;
using Neo.IO.Caching;
using Neo.Persistence;
using Nito.Disposables;
using Xunit;
using Xunit.Extensions;

namespace test.bctklib3
{
    using RocksDbStore = Neo.Plugins.Storage.RocksDbStore;

    public class PersistenceTest
    {
        [Theory, MemberData(nameof(EmptyStores))]
        public void TestEmptyStores(IStore store, IDisposable disposable)
        {
            using (disposable)
            using (store)
            {
                var ret = store.TryGet(new byte[] { 0x01, 0x02 });
                Assert.Null(ret);

                store.Put(new byte[] { 0x01, 0x02 }, new byte[] { 0x03, 0x04 });
                ret = store.TryGet(new byte[] { 0x01, 0x02 });
                Assert.Equal(new byte[] { 0x03, 0x04 }, ret);

                store.Delete(new byte[] { 0x01, 0x02 });

                ret = store.TryGet(new byte[] { 0x01, 0x02 });
                Assert.Null(ret);
            }
        }

        public static IEnumerable<object[]> EmptyStores
        {
            get
            {
                yield return new object[] { new MemoryStore(), NoopDisposable.Instance };
                yield return new object[] { new CheckpointStore(NullReadOnlyStore.Instance), NoopDisposable.Instance };
                var (tempPath, disposable) = GetTempPath();
                yield return new object[] { RocksDbStore.Open(tempPath), disposable };
            }
        }

        [Theory, MemberData(nameof(SeekStores1))]
        public void TestSeek1Forward(IStore store, IDisposable disposable)
        {
            using (disposable)
            using (store)
            {
                var enumerator = store.Seek(new byte[] { 0x00, 0x00, 0x02 }, SeekDirection.Forward).GetEnumerator();
                Assert.True(enumerator.MoveNext());
                Assert.Equal(new byte[] { 0x00, 0x00, 0x02 }, enumerator.Current.Key);
                Assert.Equal(new byte[] { 0x02 }, enumerator.Current.Value);
                Assert.True(enumerator.MoveNext());
                Assert.Equal(new byte[] { 0x00, 0x00, 0x03 }, enumerator.Current.Key);
                Assert.Equal(new byte[] { 0x03 }, enumerator.Current.Value);
            }
        }

        [Theory, MemberData(nameof(SeekStores1))]
        public void TestSeek1Backwards(IStore store, IDisposable disposable)
        {
            using (disposable)
            using (store)
            {
                var enumerator = store.Seek(new byte[] { 0x00, 0x00, 0x02 }, SeekDirection.Backward).GetEnumerator();
                Assert.True(enumerator.MoveNext());
                Assert.Equal(new byte[] { 0x00, 0x00, 0x02 }, enumerator.Current.Key);
                Assert.Equal(new byte[] { 0x02 }, enumerator.Current.Value);
                Assert.True(enumerator.MoveNext());
                Assert.Equal(new byte[] { 0x00, 0x00, 0x01 }, enumerator.Current.Key);
                Assert.Equal(new byte[] { 0x01 }, enumerator.Current.Value);
            }
        }

        public static IEnumerable<object[]> SeekStores1
        {
            get
            {
                var memStore = new MemoryStore();
                ConfigureStore(memStore);
                yield return new object[] { memStore, NoopDisposable.Instance };
                yield return new object[] { new CheckpointStore(memStore), NoopDisposable.Instance };

                IStore cpStore = new CheckpointStore(NullReadOnlyStore.Instance);
                ConfigureStore(cpStore);
                yield return new object[] { cpStore, NoopDisposable.Instance };

                memStore = new MemoryStore();
                ConfigureStore(memStore);
                memStore.Put(new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0xFF });
                memStore.Put(new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0xFF });
                cpStore = new CheckpointStore(memStore);
                cpStore.Put(new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                cpStore.Put(new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0x03 });
                yield return new object[] { cpStore, NoopDisposable.Instance };

                var (tempPath, disposable) = GetTempPath();
                IStore rocksStore = RocksDbStore.Open(tempPath);
                ConfigureStore(rocksStore);
                yield return new object[] { rocksStore, disposable };

                static void ConfigureStore(IStore store)
                {
                    store.Put(new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                    store.Put(new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                    store.Put(new byte[] { 0x00, 0x00, 0x02 }, new byte[] { 0x02 });
                    store.Put(new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0x03 });
                    store.Put(new byte[] { 0x00, 0x00, 0x04 }, new byte[] { 0x04 });
                }
            }
        }

        [Theory, MemberData(nameof(SeekStores2))]
        public void TestSeek2Backwards(IStore store, IDisposable disposable)
        {
            using (disposable)
            using (store)
            {
                var enumerator = store.Seek(new byte[] { 0x00, 0x00, 0x03 }, SeekDirection.Backward).GetEnumerator();
                Assert.True(enumerator.MoveNext());
                Assert.Equal(new byte[] { 0x00, 0x00, 0x01 }, enumerator.Current.Key);
                Assert.Equal(new byte[] { 0x01 }, enumerator.Current.Value);
                Assert.True(enumerator.MoveNext());
                Assert.Equal(new byte[] { 0x00, 0x00, 0x00 }, enumerator.Current.Key);
                Assert.Equal(new byte[] { 0x00 }, enumerator.Current.Value);
            }
        }

        public static IEnumerable<object[]> SeekStores2
        {
            get
            {
                var memStore = new MemoryStore();
                ConfigureStore(memStore);

                yield return new object[] { memStore, NoopDisposable.Instance };
                yield return new object[] { new CheckpointStore(memStore), NoopDisposable.Instance };

                IStore cpStore = new CheckpointStore(NullReadOnlyStore.Instance);
                ConfigureStore(cpStore);
                yield return new object[] { cpStore, NoopDisposable.Instance };

                memStore = new MemoryStore();
                ConfigureStore(memStore);
                memStore.Put(new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0xFF });
                cpStore = new CheckpointStore(memStore);
                cpStore.Put(new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                yield return new object[] { cpStore, NoopDisposable.Instance };

                var (tempPath, disposable) = GetTempPath();
                IStore rocksStore = RocksDbStore.Open(tempPath);
                ConfigureStore(rocksStore);
                yield return new object[] { rocksStore, disposable };

                static void ConfigureStore(IStore store)
                {
                    store.Put(new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                    store.Put(new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                    store.Put(new byte[] { 0x00, 0x01, 0x02 }, new byte[] { 0x02 });
                }

            }
        }

        private static (string path, IDisposable disposable) GetTempPath()
        {
            string tempPath;
            do
            {
                tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(tempPath));

            var disposable = AnonymousDisposable.Create(() =>
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }
            });

            return (tempPath, disposable);
        }
    }
}
