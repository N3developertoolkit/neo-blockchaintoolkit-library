using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Models;
using Neo.IO;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;

namespace Neo.BlockchainToolkit.Persistence
{
    public sealed partial class StateServiceStore : IReadOnlyStore, IDisposable
    {
        public interface ICacheClient : IDisposable
        {
            bool TryGetCachedStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[]? value);
            void CacheStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, byte[]? value);
            bool TryGetCachedFoundStates(UInt160 contractHash, byte? prefix, out IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> value);
            void CacheFoundState(UInt160 contractHash, byte? prefix, ReadOnlyMemory<byte> key, byte[] value);
        }

        public const string LoggerCategory = "Neo.BlockchainToolkit.Persistence.StateServiceStore";
        readonly static DiagnosticSource logger;

        static StateServiceStore()
        {
            logger = new DiagnosticListener(LoggerCategory);
        }

        const byte Ledger_Prefix_BlockHash = 9;
        const byte Ledger_Prefix_CurrentBlock = 12;
        const byte Ledger_Prefix_Block = 5;
        const byte Ledger_Prefix_Transaction = 11;
        const byte NEO_Prefix_Candidate = 33;
        const byte NEO_Prefix_GasPerBlock = 29;
        const byte NEO_Prefix_VoterRewardPerCommittee = 23;

        readonly RpcClient rpcClient;
        readonly ICacheClient cacheClient;
        readonly BranchInfo branchInfo;
        readonly IReadOnlyDictionary<int, UInt160> contractMap;
        readonly IReadOnlyDictionary<UInt160, string> contractNameMap;
        bool disposed = false;

        public ProtocolSettings Settings => branchInfo.ProtocolSettings;

        public StateServiceStore(Uri uri, in BranchInfo branchInfo)
            : this(new RpcClient(uri), new MemoryCacheClient(), branchInfo)
        {
        }

        public StateServiceStore(RpcClient rpcClient, ICacheClient cacheClient, in BranchInfo branchInfo)
        {
            this.rpcClient = rpcClient;
            this.cacheClient = cacheClient;
            this.branchInfo = branchInfo;
            contractMap = branchInfo.Contracts.ToDictionary(c => c.Id, c => c.Hash);
            contractNameMap = branchInfo.Contracts.ToDictionary(c => c.Hash, c => c.Name);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                rpcClient.Dispose();
                cacheClient.Dispose();
                disposed = true;
            }
        }

        public static async Task<BranchInfo> GetBranchInfoAsync(RpcClient rpcClient, uint index)
        {
            var versionTask = rpcClient.GetVersionAsync();
            var blockHashTask = rpcClient.GetBlockHashAsync(index);
            var stateRoot = await rpcClient.GetStateRootAsync(index).ConfigureAwait(false);
            var contractsTask = GetContracts(rpcClient, stateRoot.RootHash);

            await Task.WhenAll(versionTask, blockHashTask, contractsTask).ConfigureAwait(false);

            var version = await versionTask.ConfigureAwait(false);
            var blockHash = await blockHashTask.ConfigureAwait(false);
            var contracts = await contractsTask.ConfigureAwait(false);

            return new BranchInfo(
                version.Protocol.Network,
                version.Protocol.AddressVersion,
                index,
                blockHash,
                stateRoot.RootHash,
                contracts);

            static async Task<IReadOnlyList<ContractInfo>> GetContracts(RpcClient rpcClient, UInt256 rootHash)
            {
                const byte ContractManagement_Prefix_Contract = 8;

                using var memoryOwner = MemoryPool<byte>.Shared.Rent(1);
                memoryOwner.Memory.Span[0] = ContractManagement_Prefix_Contract;
                var prefix = memoryOwner.Memory[..1];

                var contracts = new List<ContractInfo>();
                var from = Array.Empty<byte>();
                while (true)
                {
                    var found = await rpcClient.FindStatesAsync(rootHash, NativeContract.ContractManagement.Hash, prefix, from.AsMemory()).ConfigureAwait(false);
                    ValidateFoundStates(rootHash, found);
                    for (int i = 0; i < found.Results.Length; i++)
                    {
                        var (key, value) = found.Results[i];
                        if (key.AsSpan().StartsWith(prefix.Span))
                        {
                            var state = new StorageItem(value).GetInteroperable<ContractState>();
                            contracts.Add(new ContractInfo(state.Id, state.Hash, state.Manifest.Name));
                        }
                    }
                    if (!found.Truncated || found.Results.Length == 0) break;
                    from = found.Results[^1].key;
                }
                return contracts;
            }
        }

        static void ValidateFoundStates(UInt256 rootHash, RpcFoundStates foundStates)
        {
            if (foundStates.Results.Length > 0)
            {
                ValidateProof(rootHash, foundStates.FirstProof, foundStates.Results[0]);
            }
            if (foundStates.Results.Length > 1)
            {
                ValidateProof(rootHash, foundStates.LastProof, foundStates.Results[^1]);
            }

            static void ValidateProof(UInt256 rootHash, byte[]? proof, (byte[] key, byte[] value) result)
            {
                var (storageKey, storageValue) = Utility.VerifyProof(rootHash, proof);
                if (!result.key.AsSpan().SequenceEqual(storageKey.Key.Span)) throw new Exception("Incorrect StorageKey");
                if (!result.value.AsSpan().SequenceEqual(storageValue)) throw new Exception("Incorrect StorageItem");
            }
        }

        public byte[]? TryGet(byte[]? _key)
        {
            if (disposed) throw new ObjectDisposedException(nameof(StateServiceStore));

            _key ??= Array.Empty<byte>();
            var contractId = BinaryPrimitives.ReadInt32LittleEndian(_key.AsSpan(0, 4));
            var key = _key.AsMemory(4);

            if (contractId == NativeContract.Ledger.Id)
            {
                // Since blocks and transactions are immutable and already available via other
                // JSON-RPC methods, the state service does not store ledger contract data.
                // StateServiceStore needs to translate LedgerContract calls into equivalent
                // non-state service calls.

                // LedgerContract stores the current block's hash and index in a single storage
                // recorded keyed by Ledger_Prefix_CurrentBlock. StateServiceStore is initialized
                // with the branching index, so TryGet needs the associated block hash for this index
                // in order to correctly construct the serialized storage
                var prefix = key.Span[0];
                if (prefix == Ledger_Prefix_CurrentBlock)
                {
                    Debug.Assert(key.Length == 1);

                    var @struct = new VM.Types.Struct() { branchInfo.IndexHash.ToArray(), branchInfo.Index };
                    return BinarySerializer.Serialize(@struct, 1024 * 1024);
                }

                // all other ledger contract prefixes (Block, BlockHash and Transaction) store immutable
                // data, so this data can be directly retrieved from LedgerContract storage
                if (prefix == Ledger_Prefix_Block
                    || prefix == Ledger_Prefix_BlockHash
                    || prefix == Ledger_Prefix_Transaction)
                {
                    return GetStorage(NativeContract.Ledger.Hash, key,
                        () => rpcClient.GetStorage(NativeContract.Ledger.Hash, key.Span));
                }

                throw new NotSupportedException(
                    $"{nameof(StateServiceStore)} does not support TryGet method for {nameof(LedgerContract)} with {Convert.ToHexString(key.Span)} key");
            }

            if (contractId == NativeContract.RoleManagement.Id)
            {
                var prefix = key.Span[0];
                if (Enum.IsDefined((Role)prefix))
                {
                    return GetFromStates(NativeContract.RoleManagement.Hash, prefix, key);
                }
            }

            if (contractId == NativeContract.NEO.Id)
            {
                var prefix = key.Span[0];
                if (prefix != NEO_Prefix_Candidate
                    && prefix != NEO_Prefix_GasPerBlock
                    && prefix != NEO_Prefix_VoterRewardPerCommittee)
                {
                    GetFromStates(NativeContract.NEO.Hash, prefix, key);
                }
            }

            var contractHash = contractMap[contractId];
            if (contractId < 0)
            {
                return GetStorage(NativeContract.Ledger.Hash, key,
                    () => rpcClient.GetProvenState(branchInfo.RootHash, contractHash, key.Span));
            }

            return GetFromStates(contractHash, null, key);

            byte[]? GetFromStates(UInt160 contractHash, byte? prefix, ReadOnlyMemory<byte> key)
            {
                return FindStates(contractHash, prefix)
                    .FirstOrDefault(kvp => MemorySequenceComparer.Equals(kvp.key.Span, key.Span)).value;
            }
        }

        byte[]? GetStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, Func<byte[]?> getStorageFromService)
        {
            if (cacheClient.TryGetCachedStorage(contractHash, key, out var value)) return value;

            const string loggerName = nameof(GetStorage);
            Activity? activity = null;
            if (logger.IsEnabled(loggerName))
            {
                activity = new Activity(loggerName);
                logger.StartActivity(activity, new { 
                    contractHash, 
                    contractName = contractNameMap[contractHash],
                    prefix = Convert.ToHexString(key.Span)});
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = getStorageFromService();
                cacheClient.CacheStorage(contractHash, key, result);
                return result;
            }
            finally
            {
                stopwatch.Stop();
                if (activity is not null) logger.StopActivity(activity, new { elapsed = stopwatch.Elapsed });
            }
        }

        public bool Contains(byte[]? key) => TryGet(key) is not null;

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? _key, SeekDirection direction)
        {
            if (disposed) throw new ObjectDisposedException(nameof(StateServiceStore));

            _key ??= Array.Empty<byte>();
            var contractId = BinaryPrimitives.ReadInt32LittleEndian(_key.AsSpan(0, 4));
            var key = _key.AsMemory(4);

            if (contractId == NativeContract.Ledger.Id)
            {
                // Because the state service does not store ledger contract data, the seek method cannot
                // be implemented for the ledger contract. As of Neo 3.4, the Ledger contract only 
                // uses Seek in the Initialized method to check for the existence of any value with a
                // Prefix_Block prefix. In order to support this single scenario, return a single empty
                // byte array enumerable. This will enable .Any() LINQ method to return true, but will
                // fail if the caller attempts to deserialize the provided array into a trimmed block.

                var prefix = key.Span[0];
                if (prefix == Ledger_Prefix_Block)
                {
                    Debug.Assert(_key.Length == 5);
                    return Enumerable.Repeat((_key, Array.Empty<byte>()), 1);
                }

                throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method for {nameof(LedgerContract)} with {prefix} prefix");
            }

            // for RoleManagement, 
            if (contractId == NativeContract.RoleManagement.Id)
            {
                var prefix = key.Span[0];
                if (!Enum.IsDefined((Role)prefix))
                {
                    throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method for {nameof(RoleManagement)} with {prefix} prefix");
                }

                var states = FindStates(NativeContract.RoleManagement.Hash, prefix);
                return ConvertStates(key, states);
            }


            if (contractId == NativeContract.NEO.Id)
            {
                var prefix = key.Span[0];
                if (prefix != NEO_Prefix_Candidate
                    && prefix != NEO_Prefix_GasPerBlock
                    && prefix != NEO_Prefix_VoterRewardPerCommittee)
                {
                    throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method for {nameof(NeoToken)} with {prefix} prefix");
                }

                var states = FindStates(NativeContract.RoleManagement.Hash, prefix);
                return ConvertStates(key, states);
            }

            if (contractId < 0)
            {
                var contract = branchInfo.Contracts.Single(c => c.Id == contractId);
                throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method for native {contract.Name} contract");
            }

            {
                var states = FindStates(contractMap[contractId]);
                return ConvertStates(key, states);
            }

            IEnumerable<(byte[] Key, byte[] Value)> ConvertStates(ReadOnlyMemory<byte> key, IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> states)
            {
                var comparer = direction == SeekDirection.Forward
                   ? MemorySequenceComparer.Default
                   : MemorySequenceComparer.Reverse;
                
                return states
                    .Where(kvp => kvp.key.Span.StartsWith(key.Span))
                    .Select(kvp =>
                    {
                        var k = new byte[kvp.key.Length + 4];
                        BinaryPrimitives.WriteInt32LittleEndian(k.AsSpan(0, 4), contractId);
                        kvp.key.CopyTo(k.AsMemory(4));
                        return (key: k, kvp.value);
                    })
                    .OrderBy(kvp => kvp.key, comparer);
            }
        }

        IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> FindStates(UInt160 contractHash, byte? prefix = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(StateServiceStore));

            if (cacheClient.TryGetCachedFoundStates(contractHash, prefix, out var values))
            {
                return values;
            }

            return FindStatesFromService(contractHash, prefix);
        }

        IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> FindStatesFromService(UInt160 contractHash, byte? prefix = null)
        {
            const string loggerName = nameof(FindStatesFromService);
            var contractName = contractNameMap[contractHash];
            Activity? activity = null;
            if (logger.IsEnabled(loggerName))
            {
                activity = new Activity(loggerName);
                logger.StartActivity(activity, new { 
                    contractHash, 
                    contractName = contractNameMap[contractHash],
                    prefix });
            }

            var count = 0;
            var stopwatch = Stopwatch.StartNew();

            ReadOnlyMemory<byte> prefixBuffer = default;
            byte[]? rentedBuffer = null;
            if (prefix.HasValue)
            {
                rentedBuffer = ArrayPool<byte>.Shared.Rent(1);
                rentedBuffer[0] = prefix.Value;
                prefixBuffer = rentedBuffer.AsMemory(0, 1);
            }

            try
            {
                var from = Array.Empty<byte>();
                while (true)
                {
                    var found = rpcClient.FindStates(branchInfo.RootHash, contractHash, prefixBuffer.Span, from);
                    ValidateFoundStates(branchInfo.RootHash, found);
                    count += found.Results.Length;
                    if (logger.IsEnabled(loggerName) && found.Truncated)
                    {
                        logger.Write($"{loggerName}.Found", new {
                            total = count,
                            found = found.Results.Length});
                    }
                    for (int i = 0; i < found.Results.Length; i++)
                    {
                        var (key, value) = found.Results[i];
                        cacheClient.CacheFoundState(contractHash, prefix, key, value);
                        yield return (key, value);
                    }
                    if (!found.Truncated || found.Results.Length == 0) break;
                    from = found.Results[^1].key;
                }
            }
            finally
            {
                stopwatch.Stop();
                if (rentedBuffer is not null) ArrayPool<byte>.Shared.Return(rentedBuffer);
                if (activity is not null) logger.StopActivity(activity, new { count, elapsed = stopwatch.Elapsed });
            }
        }
    }
}
