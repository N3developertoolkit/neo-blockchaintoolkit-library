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
    public class PersistenceTest
    {
        [Theory, MemberData(nameof(EmptyStores))]
        public void TestEmptyStores(IStore store, IDisposable disposable)
        {
            using (disposable)
            using (store)
            {
                var ret = store.TryGet(0, new byte[] { 0x01, 0x02 });
                Assert.Null(ret);

                store.Put(0, new byte[] { 0x01, 0x02 }, new byte[] { 0x03, 0x04 });
                ret = store.TryGet(0, new byte[] { 0x01, 0x02 });
                Assert.Equal(new byte[] { 0x03, 0x04 }, ret);

                ret = store.TryGet(1, new byte[] { 0x01, 0x02 });
                Assert.Null(ret);

                store.Delete(0, new byte[] { 0x01, 0x02 });

                ret = store.TryGet(0, new byte[] { 0x01, 0x02 });
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
                var enumerator = store.Seek(1, new byte[] { 0x00, 0x00, 0x02 }, SeekDirection.Forward).GetEnumerator();
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
                var enumerator = store.Seek(1, new byte[] { 0x00, 0x00, 0x02 }, SeekDirection.Backward).GetEnumerator();
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
                memStore.Put(1, new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                memStore.Put(1, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                memStore.Put(1, new byte[] { 0x00, 0x00, 0x02 }, new byte[] { 0x02 });
                memStore.Put(1, new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0x03 });
                memStore.Put(1, new byte[] { 0x00, 0x00, 0x04 }, new byte[] { 0x04 });
                yield return new object[] { memStore, NoopDisposable.Instance };
                yield return new object[] { new CheckpointStore(memStore), NoopDisposable.Instance };

                IStore cpStore = new CheckpointStore(NullReadOnlyStore.Instance);
                cpStore.Put(1, new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                cpStore.Put(1, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                cpStore.Put(1, new byte[] { 0x00, 0x00, 0x02 }, new byte[] { 0x02 });
                cpStore.Put(1, new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0x03 });
                cpStore.Put(1, new byte[] { 0x00, 0x00, 0x04 }, new byte[] { 0x04 });
                yield return new object[] { cpStore, NoopDisposable.Instance };

                memStore = new MemoryStore();
                memStore.Put(1, new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                memStore.Put(1, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0xFF });
                memStore.Put(1, new byte[] { 0x00, 0x00, 0x02 }, new byte[] { 0x02 });
                memStore.Put(1, new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0xFF });
                memStore.Put(1, new byte[] { 0x00, 0x00, 0x04 }, new byte[] { 0x04 });
                cpStore = new CheckpointStore(memStore);
                cpStore.Put(1, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                cpStore.Put(1, new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0x03 });
                yield return new object[] { cpStore, NoopDisposable.Instance };

                var (tempPath, disposable) = GetTempPath();
                IStore rocksStore = RocksDbStore.Open(tempPath);
                rocksStore.Put(1, new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                rocksStore.Put(1, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                rocksStore.Put(1, new byte[] { 0x00, 0x00, 0x02 }, new byte[] { 0x02 });
                rocksStore.Put(1, new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0x03 });
                rocksStore.Put(1, new byte[] { 0x00, 0x00, 0x04 }, new byte[] { 0x04 });
                yield return new object[] { rocksStore, disposable };
            }
        }

        [Theory, MemberData(nameof(SeekStores2))]
        public void TestSeek2Backwards(IStore store, IDisposable disposable)
        {
            using (disposable)
            using (store)
            {
                var enumerator = store.Seek(2, new byte[] { 0x00, 0x00, 0x03 }, SeekDirection.Backward).GetEnumerator();
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
                memStore.Put(2, new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                memStore.Put(2, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                memStore.Put(2, new byte[] { 0x00, 0x01, 0x02 }, new byte[] { 0x02 });

                yield return new object[] { memStore, NoopDisposable.Instance };
                yield return new object[] { new CheckpointStore(memStore), NoopDisposable.Instance };

                IStore cpStore = new CheckpointStore(NullReadOnlyStore.Instance);
                cpStore.Put(2, new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                cpStore.Put(2, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                cpStore.Put(2, new byte[] { 0x00, 0x01, 0x02 }, new byte[] { 0x02 });
                yield return new object[] { cpStore, NoopDisposable.Instance };

                memStore = new MemoryStore();
                memStore.Put(2, new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                memStore.Put(2, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0xFF });
                memStore.Put(2, new byte[] { 0x00, 0x01, 0x02 }, new byte[] { 0x02 });
                cpStore = new CheckpointStore(memStore);
                cpStore.Put(2, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                yield return new object[] { cpStore, NoopDisposable.Instance };

                var (tempPath, disposable) = GetTempPath();
                IStore rocksStore = RocksDbStore.Open(tempPath);
                rocksStore.Put(2, new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                rocksStore.Put(2, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                rocksStore.Put(2, new byte[] { 0x00, 0x01, 0x02 }, new byte[] { 0x02 });
                yield return new object[] { rocksStore, disposable };
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

            var disposable = AnonymousDisposable.Create(() => {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }
            });

            return (tempPath, disposable);
        }
    }
}
