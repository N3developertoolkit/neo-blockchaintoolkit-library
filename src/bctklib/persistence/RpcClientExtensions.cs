using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Models;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Native;

namespace Neo.BlockchainToolkit.Persistence
{
    static class RpcClientExtensions
    {

        public static async Task<BranchInfo> GetBranchInfoAsync(this RpcClient rpcClient, uint index)
        {
            var version = await rpcClient.GetVersionAsync().ConfigureAwait(false);
            var stateRoot = await rpcClient.GetStateRootAsync(index).ConfigureAwait(false);
            var blockHash = await rpcClient.GetBlockHashAsync(index).ConfigureAwait(false);
            var contractMap = await GetContractMap(rpcClient, stateRoot.RootHash);
            return new BranchInfo(
                version.Protocol.Network,
                version.Protocol.AddressVersion,
                index,
                blockHash,
                stateRoot.RootHash,
                contractMap);

            static async Task<IReadOnlyDictionary<int, UInt160>> GetContractMap(RpcClient rpcClient, UInt256 rootHash)
            {
                const byte ContractManagement_Prefix_Contract = 8;

                using var memoryOwner = MemoryPool<byte>.Shared.Rent(1);
                memoryOwner.Memory.Span[0] = ContractManagement_Prefix_Contract;
                var prefix = memoryOwner.Memory.Slice(0, 1);

                var contractMapBuilder = ImmutableDictionary.CreateBuilder<int, UInt160>();
                var enumerable = rpcClient.EnumerateStatesAsync(rootHash, NativeContract.ContractManagement.Hash, prefix);
                await foreach (var (key, value) in enumerable)
                {
                    if (key.AsSpan().StartsWith(prefix.Span))
                    {
                        var state = new StorageItem(value).GetInteroperable<ContractState>();
                        contractMapBuilder.Add(state.Id, state.Hash);
                    }
                }
                return contractMapBuilder.ToImmutable();
            }
        }

        public static RpcVersion GetVersion(this RpcClient rpcClient)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName());
            return RpcVersion.FromJson((Json.JObject)result);
        }

        public static UInt256 GetBlockHash(this RpcClient rpcClient, uint index)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), index);
            return UInt256.Parse(result.AsString());
        }


        // TODO: remove when https://github.com/neo-project/neo-modules/issues/756 is resolved
        public static async Task<UInt256> GetBlockHashAsync(this RpcClient rpcClient, uint index)
        {
            var result = await rpcClient.RpcSendAsync("getblockhash", index).ConfigureAwait(false);
            return UInt256.Parse(result.AsString());
        }


        public static byte[] GetProof(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
        {
            var result = rpcClient.RpcSend(
                RpcClient.GetRpcName(),
                rootHash.ToString(),
                scriptHash.ToString(),
                Convert.ToBase64String(key));
            return Convert.FromBase64String(result.AsString());
        }

        public static byte[]? GetProvenState(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
        {
            const int COR_E_KEYNOTFOUND = unchecked((int)0x80131577);

            try
            {
                var result = rpcClient.GetProof(rootHash, scriptHash, key);
                return Utility.VerifyProof(rootHash, result).value;
            }
            // GetProvenState has to match the semantics of IReadOnlyStore.TryGet
            // which returns null for invalid keys instead of throwing an exception.
            catch (RpcException ex) when (ex.HResult == COR_E_KEYNOTFOUND)
            {
                // Trie class throws KeyNotFoundException if key is not in the trie.
                // RpcClient/Server converts the KeyNotFoundException into an
                // RpcException with code == COR_E_KEYNOTFOUND.

                return null;
            }
            catch (RpcException ex) when (ex.HResult == -100 && ex.Message == "Unknown value")
            {
                // Prior to Neo 3.3.0, StateService GetProof method threw a custom exception 
                // instead of KeyNotFoundException like GetState. This catch clause detected
                // the custom exception that GetProof used to throw. 

                // TODO: remove this clause once deployed StateService for Neo N3 MainNet and
                //       TestNet has been verified to be running Neo 3.3.0 or later.

                return null;
            }
        }

        public static byte[] GetStorage(this RpcClient rpcClient, UInt160 contractHash, ReadOnlySpan<byte> key)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), contractHash.ToString(), Convert.ToBase64String(key));
            return Convert.FromBase64String(result.AsString());
        }

        public static RpcStateRoot GetStateRoot(this RpcClient rpcClient, uint index)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), index);
            return RpcStateRoot.FromJson((Json.JObject)result);
        }

        public static async Task<RpcStateRoot> GetStateRootAsync(this RpcClient rpcClient, uint index)
        {
            var result = await rpcClient.RpcSendAsync(RpcClient.GetRpcName(), index).ConfigureAwait(false);
            return RpcStateRoot.FromJson((Json.JObject)result);
        }

        public static RpcFoundStates FindStates(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> from = default, int? count = null)
        {
            var @params = StateAPI.MakeFindStatesParams(rootHash, scriptHash, prefix, from, count);
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), @params);
            return RpcFoundStates.FromJson((Json.JObject)result);
        }

        public static async Task<RpcFoundStates> FindStatesAsync(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default, int? count = null)
        {
            var @params = StateAPI.MakeFindStatesParams(rootHash, scriptHash, prefix.Span, from.Span, count);
            var result = await rpcClient.RpcSendAsync(RpcClient.GetRpcName(), @params).ConfigureAwait(false);
            return RpcFoundStates.FromJson((Json.JObject)result);
        }

        public static IEnumerable<(byte[] key, byte[] value)> EnumerateStates(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, int? pageSize = null)
        {
            var from = Array.Empty<byte>();
            while (true)
            {
                var foundStates = rpcClient.FindStates(rootHash, scriptHash, prefix.Span, from, pageSize);
                var states = ValidateFoundStates(rootHash, foundStates);
                for (int i = 0; i < states.Length; i++)
                {
                    yield return (states[i].key, states[i].value);
                }
                if (!foundStates.Truncated || states.Length == 0) break;
                from = states[^1].key;
            }
        }

        internal static IEnumerable<(byte[] key, byte[] value)> EnumerateStates(this StateServiceStore.ICachingClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, int? pageSize = null)
        {
            var from = Array.Empty<byte>();
            while (true)
            {
                var foundStates = rpcClient.FindStates(rootHash, scriptHash, prefix, from, pageSize);
                var states = ValidateFoundStates(rootHash, foundStates);
                for (int i = 0; i < states.Length; i++)
                {
                    yield return (states[i].key, states[i].value);
                }
                if (!foundStates.Truncated || states.Length == 0) break;
                from = states[^1].key;
            }
        }


        public static async IAsyncEnumerable<(byte[] key, byte[] value)> EnumerateStatesAsync(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, int? pageSize = null)
        {
            var from = Array.Empty<byte>();
            while (true)
            {
                var foundStates = await rpcClient.FindStatesAsync(rootHash, scriptHash, prefix, from.AsMemory(), pageSize).ConfigureAwait(false);
                var states = ValidateFoundStates(rootHash, foundStates);
                for (int i = 0; i < states.Length; i++)
                {
                    yield return (states[i].key, states[i].value);
                }
                if (!foundStates.Truncated || states.Length == 0) break;
                from = states[^1].key;
            }
        }

        static (byte[] key, byte[] value)[] ValidateFoundStates(UInt256 rootHash, Network.RPC.Models.RpcFoundStates foundStates)
        {
            if (foundStates.Results.Length > 0)
            {
                ValidateProof(rootHash, foundStates.FirstProof, foundStates.Results[0]);
            }
            if (foundStates.Results.Length > 1)
            {
                ValidateProof(rootHash, foundStates.LastProof, foundStates.Results[^1]);
            }
            return foundStates.Results;

            static void ValidateProof(UInt256 rootHash, byte[]? proof, (byte[] key, byte[] value) result)
            {
                var (storageKey, storageValue) = Utility.VerifyProof(rootHash, proof);
                if (!result.key.AsSpan().SequenceEqual(storageKey.Key.Span)) throw new Exception("Incorrect StorageKey");
                if (!result.value.AsSpan().SequenceEqual(storageValue)) throw new Exception("Incorrect StorageItem");
            }
        }
    }
}
