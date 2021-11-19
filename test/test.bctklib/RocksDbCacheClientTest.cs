using System;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.IO.Json;
using Neo.Network.RPC;
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
            var key = Neo.Utility.StrictUTF8.GetBytes("key");
            var expected = Neo.Utility.StrictUTF8.GetBytes("this is a test");

            using var rpcClient = new TestableRpcClient(() => Convert.ToBase64String(expected));

            var tempPath = RocksDbUtility.GetTempPath();
            using var _ = Utility.GetDeleteDirectoryDisposable(tempPath);
            using var client = new StateServiceStore.RocksDbCacheClient(rpcClient, tempPath);

            var actual1 = client.GetState(UInt256.Zero, UInt160.Zero, key);
            var actual2 = client.GetState(UInt256.Zero, UInt160.Zero, key);
            Assert.Equal(expected, actual1);
            Assert.Equal(expected, actual2);
        }

        [Fact]
        public void cached_get_state_returns_null_for_key_not_found_exception()
        {
            var key = Neo.Utility.StrictUTF8.GetBytes("key");

            using var rpcClient = new TestableRpcClient(() => throw new RpcException(-2146232969, "The given key was not present in the dictionary."));

            var tempPath = RocksDbUtility.GetTempPath();
            using var _ = Utility.GetDeleteDirectoryDisposable(tempPath);
            using var client = new StateServiceStore.RocksDbCacheClient(rpcClient, tempPath);

            var actual1 = client.GetState(UInt256.Zero, UInt160.Zero, key);
            var actual2 = client.GetState(UInt256.Zero, UInt160.Zero, key);
            Assert.Null(actual1);
            Assert.Null(actual2);
        }
    }
}
