using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using Nito.Disposables;
using OneOf;
using Xunit;

namespace test.bctklib3
{
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
        public void TestSeek1Forward(IExpressStore store, IDisposable disposable)
        {
            using (disposable)
            using (store)
            {
                var enumerator = store.Seek(default, new byte[] { 0x00, 0x00, 0x02 }, SeekDirection.Forward).GetEnumerator();
                Assert.True(enumerator.MoveNext());
                Assert.Equal(new byte[] { 0x00, 0x00, 0x02 }, enumerator.Current.Key);
                Assert.Equal(new byte[] { 0x02 }, enumerator.Current.Value);
                Assert.True(enumerator.MoveNext());
                Assert.Equal(new byte[] { 0x00, 0x00, 0x03 }, enumerator.Current.Key);
                Assert.Equal(new byte[] { 0x03 }, enumerator.Current.Value);
            }
        }

        [Theory, MemberData(nameof(SeekStores1))]
        public void TestSeek1Backwards(IExpressStore store, IDisposable disposable)
        {
            using (disposable)
            using (store)
            {
                var enumerator = store.Seek(default, new byte[] { 0x00, 0x00, 0x02 }, SeekDirection.Backward).GetEnumerator();
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
                var memStore = new TestMemoryStore();
                ConfigureStore(memStore.Put);
                yield return new object[] { memStore, NoopDisposable.Instance };
                yield return new object[] { new CheckpointStore(memStore), NoopDisposable.Instance };

                var cpStore = new CheckpointStore(NullReadOnlyStore.Instance);
                ConfigureStore(cpStore.Put);
                yield return new object[] { cpStore, NoopDisposable.Instance };

                memStore = new TestMemoryStore();
                ConfigureStore(memStore.Put);
                memStore.Put(default, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0xFF });
                memStore.Put(default, new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0xFF });
                cpStore = new CheckpointStore(memStore);
                cpStore.Put(default, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                cpStore.Put(default, new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0x03 });
                yield return new object[] { cpStore, NoopDisposable.Instance };

                var (tempPath, disposable) = GetTempPath();
                var rocksStore = RocksDbStore.Open(tempPath);
                ConfigureStore(rocksStore.Put);
                yield return new object[] { rocksStore, disposable };

                static void ConfigureStore(Action<byte, byte[]?, byte[]> put)
                {
                    put(default, new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                    put(default, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                    put(default, new byte[] { 0x00, 0x00, 0x02 }, new byte[] { 0x02 });
                    put(default, new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0x03 });
                    put(default, new byte[] { 0x00, 0x00, 0x04 }, new byte[] { 0x04 });
                }
            }
        }

        [Theory, MemberData(nameof(SeekStores2))]
        public void TestSeek2Backwards(IExpressStore store, IDisposable disposable)
        {
            using (disposable)
            using (store)
            {
                var enumerator = store.Seek(default, new byte[] { 0x00, 0x00, 0x03 }, SeekDirection.Backward).GetEnumerator();
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
                var memStore = new TestMemoryStore();
                ConfigureStore(memStore.Put);

                yield return new object[] { memStore, NoopDisposable.Instance };
                yield return new object[] { new CheckpointStore(memStore), NoopDisposable.Instance };

                var cpStore = new CheckpointStore(NullReadOnlyStore.Instance);
                ConfigureStore(cpStore.Put);
                yield return new object[] { cpStore, NoopDisposable.Instance };

                memStore = new TestMemoryStore();
                ConfigureStore(memStore.Put);
                memStore.Put(default, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0xFF });
                cpStore = new CheckpointStore(memStore);
                cpStore.Put(default, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                yield return new object[] { cpStore, NoopDisposable.Instance };

                var (tempPath, disposable) = GetTempPath();
                var rocksStore = RocksDbStore.Open(tempPath);
                ConfigureStore(rocksStore.Put);
                yield return new object[] { rocksStore, disposable };

                static void ConfigureStore(Action<byte, byte[]?, byte[]> put)
                {
                    put(default, new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                    put(default, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                    put(default, new byte[] { 0x00, 0x01, 0x02 }, new byte[] { 0x02 });
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
