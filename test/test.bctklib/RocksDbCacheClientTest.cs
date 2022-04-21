using System;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.Cryptography.MPTTrie;
using Neo.IO.Json;
using Neo.Network.RPC;
using Neo.SmartContract;
using RocksDbSharp;
using Xunit;

namespace test.bctklib
{
    public class RocksDbCacheClientTest
    {
        WriteOptions syncWriteOptions = new WriteOptions().SetSync(true);

        [Fact]
        public void cached_get_state_returns_expected()
        {
            using var store = new Neo.Persistence.MemoryStore();
            var trie = Utility.GetTestTrie(store);
            var key = Utility.MakeTestTrieKey(42);
            var proof = trie.GetSerializedProof(key);
            var expected = trie.GetValue(key).Value;

            using var rpcClient = new TestableRpcClient(() => Convert.ToBase64String(trie.GetSerializedProof(key)));

            var tempPath = RocksDbUtility.GetTempPath();
            using var _ = Utility.GetDeleteDirectoryDisposable(tempPath);
            using var client = new StateServiceStore.RocksDbCacheClient(rpcClient, tempPath);

            Assert.Null(client.GetCachedState(trie.Root.Hash, UInt160.Zero, key.Key));
            var actual1 = client.GetState(trie.Root.Hash, UInt160.Zero, key.Key);
            Assert.Equal(expected, actual1);
            Assert.NotNull(client.GetCachedState(trie.Root.Hash, UInt160.Zero, key.Key));

            var actual2 = client.GetState(trie.Root.Hash, UInt160.Zero, key.Key);
            Assert.Equal(expected, actual2);
        }

        [Theory]
        [InlineData(-2146232969, "The given key was not present in the dictionary.")]
        [InlineData(-2146232969, "Halt and catch fire.")]
        [InlineData(-100, "Unknown value")]
        public void cached_get_state_returns_null_for_key_not_found_exception(int code, string msg)
        {
            var key = Neo.Utility.StrictUTF8.GetBytes("key");

            using var rpcClient = new TestableRpcClient(() => throw new RpcException(code, msg));

            var tempPath = RocksDbUtility.GetTempPath();
            using var _ = Utility.GetDeleteDirectoryDisposable(tempPath);
            using var client = new StateServiceStore.RocksDbCacheClient(rpcClient, tempPath);

            Assert.Null(client.GetCachedState(UInt256.Zero, UInt160.Zero, key));
            var actual1 = client.GetState(UInt256.Zero, UInt160.Zero, key);
            Assert.Null(actual1);
            Assert.NotNull(client.GetCachedState(UInt256.Zero, UInt160.Zero, key));

            var actual2 = client.GetState(UInt256.Zero, UInt160.Zero, key);
            Assert.Null(actual2);
        }

        // [Fact]
        // public void cached_get_state_returns_null_for_key_not_found_exception_workaround()
        // {
        //     var key = Neo.Utility.StrictUTF8.GetBytes("key");

        //     using var rpcClient = new TestableRpcClient(() => throw new RpcException(-2146232969, "The given key was not present in the dictionary."));

        //     var tempPath = RocksDbUtility.GetTempPath();
        //     using var _ = Utility.GetDeleteDirectoryDisposable(tempPath);
        //     using var client = new StateServiceStore.RocksDbCacheClient(rpcClient, tempPath);

        //     var actual1 = client.GetState(UInt256.Zero, UInt160.Zero, key);
        //     var actual2 = client.GetState(UInt256.Zero, UInt160.Zero, key);
        //     Assert.Null(actual1);
        //     Assert.Null(actual2);
        // }
    }
}
