using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Persistence;
using Neo.Cryptography.MPTTrie;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.RPC;
using Neo.SmartContract;
using Xunit;

namespace test.bctklib
{
    public class RpcClientExtensionsTest
    {
        [Fact]
        public void get_state_returns_expected()
        {
            using var store = new Neo.Persistence.MemoryStore();
            var trie = Utility.GetTestTrie(store);
            var key = Utility.MakeTestTrieKey(42);
            var expected = trie.GetValue(key).Value;

            var rpcClient = new TestableRpcClient(() => Convert.ToBase64String(trie.GetSerializedProof(key)));
            var actual = rpcClient.GetProvenState(trie.Root.Hash, UInt160.Zero, default);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(-2146232969, "The given key was not present in the dictionary.")]
        [InlineData(-2146232969, "Halt and catch fire.")]
        [InlineData(-100, "Unknown value")]
        public void GetProvenState_returns_null_for_key_not_found_exception(int code, string msg)
        {
            var rpcClient = new TestableRpcClient(() => throw new RpcException(code, msg));
            var actual = rpcClient.GetProvenState(UInt256.Zero, UInt160.Zero, default);
            Assert.Null(actual);
        }

        [Theory]
        [InlineData(-100, "The given key was not present in the dictionary.")]
        [InlineData(-200, "Unknown value")]
        public void GetProvenState_throws_for_other_exception(int code, string msg)
        {
            var rpcClient = new TestableRpcClient(() => throw new RpcException(code, msg));
            Assert.Throws<RpcException>(() => rpcClient.GetProvenState(UInt256.Zero, UInt160.Zero, default));
        }

        static UInt256 rootHash = UInt256.Parse("0xaf05f13fc9e176aae66e06c0fe0b4258b0340e4f4532fe6a808ee65480f10196");
        static UInt160 contractHash = UInt160.Parse("0x45d2db3c857b6c33d0cb7d689b35a752d1e5e6bb");

        [Fact]
        public void test_FindStates()
        {
            using var rpcClient = new TestableRpcClient();
            rpcClient.QueueResource("FindStates.json");

            var states = rpcClient.FindStates(rootHash, contractHash, default);
            Assert.NotNull(states);
        }

        [Fact]
        public void test_GetProvenState()
        {
            using var rpcClient = new TestableRpcClient();
            rpcClient.QueueResource("getproof.json");

            var key = Neo.Utility.StrictUTF8.GetBytes("sample.domain");
            var expected = UInt160.Parse("0x06cb35134fce60a8c6445a608b5e43fe827d349e");
            var state = rpcClient.GetProvenState(rootHash, contractHash, key);
            Assert.NotNull(state);

            var actual = new UInt160(state);
            Assert.Equal(expected, actual);
        }



        // [Fact]
        // public void doo()
        // {
        //     var rootHash = UInt256.Zero;
        //     {
        //         using var snapshot = memoryStore.GetSnapshot();
        //         rootHash = trie.Root.Hash;
        //     }

        //     {
        //         var key = new StorageKey() { Id = 1, Key = BitConverter.GetBytes(42) };

        //         using var snapshot = memoryStore.GetSnapshot();
        //         var trie = new Trie<StorageKey, StorageItem>(snapshot, rootHash);
        //         var proof = trie.GetSerializedProof(key);
        //     }
        // }
    }
}
