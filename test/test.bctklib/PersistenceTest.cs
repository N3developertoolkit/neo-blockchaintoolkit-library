// using System;
// using System.Collections.Concurrent;
// using System.Collections.Generic;
// using System.IO;
// using Neo.BlockchainToolkit.Persistence;
// using Neo.Persistence;
// using Neo.Plugins;
// using Nito.Disposables;
// using Xunit;

// namespace test.bctklib3
// {
//     public class PersistenceTest
//     {
//         [Theory, MemberData(nameof(SeekStores1))]
//         public void TestSeek1Forward(IStorageProvider storageProvider, IDisposable? disposable)
//         {
//             using (disposable)
//             using (storageProvider as IDisposable)
//             {
//                 var store = storageProvider.GetStore(nameof(SeekStores1));
//                 var enumerator = store.Seek(new byte[] { 0x00, 0x00, 0x02 }, SeekDirection.Forward).GetEnumerator();
//                 Assert.True(enumerator.MoveNext());
//                 Assert.Equal(new byte[] { 0x00, 0x00, 0x02 }, enumerator.Current.Key);
//                 Assert.Equal(new byte[] { 0x02 }, enumerator.Current.Value);
//                 Assert.True(enumerator.MoveNext());
//                 Assert.Equal(new byte[] { 0x00, 0x00, 0x03 }, enumerator.Current.Key);
//                 Assert.Equal(new byte[] { 0x03 }, enumerator.Current.Value);
//             }
//         }

//         [Theory, MemberData(nameof(SeekStores1))]
//         public void TestSeek1Backwards(IStorageProvider storageProvider, IDisposable? disposable)
//         {
//             using (disposable)
//             using (storageProvider as IDisposable)
//             {
//                 var store = storageProvider.GetStore(nameof(SeekStores1));
//                 var enumerator = store.Seek(new byte[] { 0x00, 0x00, 0x02 }, SeekDirection.Backward).GetEnumerator();
//                 Assert.True(enumerator.MoveNext());
//                 Assert.Equal(new byte[] { 0x00, 0x00, 0x02 }, enumerator.Current.Key);
//                 Assert.Equal(new byte[] { 0x02 }, enumerator.Current.Value);
//                 Assert.True(enumerator.MoveNext());
//                 Assert.Equal(new byte[] { 0x00, 0x00, 0x01 }, enumerator.Current.Key);
//                 Assert.Equal(new byte[] { 0x01 }, enumerator.Current.Value);
//             }
//         }

//         class MemoryStorageProvider : IStorageProvider, IDisposable
//         {
//             ConcurrentDictionary<string, MemoryStore> stores = new ConcurrentDictionary<string, MemoryStore>();

//             public void Dispose()
//             {
//             }

//             public IStore GetStore(string path)
//             {
//                 return stores.GetOrAdd(path, _ => new MemoryStore());
//             }
//         }

//         public static IEnumerable<object[]> SeekStores1
//         {
//             get
//             {
//                 var memProvider = new MemoryStorageProvider();
//                 yield return ConfigureStore(memProvider, null);

//                 var cpProvider = new CheckpointStorageProvider(null);
//                 yield return ConfigureStore(cpProvider, null);

//                 var (tempPath1, disposable1) = GetTempPath();
//                 var rocksProvider = RocksDbStorageProvider.Open(tempPath1);
//                 yield return ConfigureStore(rocksProvider, disposable1);

//                 var (tempPath2, disposable2) = GetTempPath();
//                 var rocksProvider2 = RocksDbStorageProvider.Open(tempPath2);
//                 using (var db1 = rocksProvider2.GetStore(nameof(SeekStores1)))
//                 {
//                     db1.PutSync(new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
//                     db1.PutSync(new byte[] { 0x00, 0x00, 0x02 }, new byte[] { 0x02 });
//                     db1.PutSync(new byte[] { 0x00, 0x00, 0x04 }, new byte[] { 0x04 });
//                 }

//                 var cpProvider2 = new CheckpointStorageProvider(rocksProvider2, checkpointCleanup: disposable2);
//                 using (var db2 = cpProvider2.GetStore(nameof(SeekStores1)))
//                 {
//                     db2.Put(new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
//                     db2.Put(new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0x03 });
//                 }
//                 yield return new object[] { cpProvider2, null! };

//                 static object[] ConfigureStore(IStorageProvider provider, IDisposable? disposable)
//                 {
//                     var store = provider.GetStore(nameof(SeekStores1));
//                     store.Put(new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
//                     store.Put(new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
//                     store.Put(new byte[] { 0x00, 0x00, 0x02 }, new byte[] { 0x02 });
//                     store.Put(new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0x03 });
//                     store.Put(new byte[] { 0x00, 0x00, 0x04 }, new byte[] { 0x04 });

//                     return new object[] { provider, disposable! };
//                 }
//             }
//         }

//         private static (string path, IDisposable disposable) GetTempPath()
//         {
//             string tempPath;
//             do
//             {
//                 tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
//             }
//             while (Directory.Exists(tempPath));

//             var disposable = AnonymousDisposable.Create(() =>
//             {
//                 if (Directory.Exists(tempPath))
//                 {
//                     Directory.Delete(tempPath, true);
//                 }
//             });

//             return (tempPath, disposable);
//         }
//     }
// }
