using System;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.IO.Json;
using Neo.Network.RPC;
using Xunit;

namespace test.bctklib3
{
    public class RpcClientExtensionsTest
    {
        [Fact]
        public void get_state_returns_expected()
        {
            var expected = Neo.Utility.StrictUTF8.GetBytes("this is a test");
            var rpcClient = new TestableRpcClient(() => Convert.ToBase64String(expected));
            var actual = rpcClient.GetState(UInt256.Zero, UInt160.Zero, default);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void get_state_returns_null_for_key_not_found_exception()
        {
            var rpcClient = new TestableRpcClient(() => throw new RpcException(-2146232969, "The given key was not present in the dictionary."));
            var actual = rpcClient.GetState(UInt256.Zero, UInt160.Zero, default);
            Assert.Null(actual);
        }

        [Fact]
        public void get_state_throws_for_other_exception()
        {
            var rpcClient = new TestableRpcClient(() => throw new RpcException(-146232969, "Halt and catch fire."));
            Assert.Throws<RpcException>(() => rpcClient.GetState(UInt256.Zero, UInt160.Zero, default));
        }
    }
}
