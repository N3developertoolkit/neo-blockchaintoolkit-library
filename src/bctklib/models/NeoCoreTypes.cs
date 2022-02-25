using System;

namespace Neo.BlockchainToolkit.Models
{
    public static class NeoCoreTypes
    {
        public static StructContractType Transaction => transaction.Value; 
        static readonly Lazy<StructContractType> transaction = new(
            () => new StructContractType(
                nameof(Transaction),
                new (string Name, ContractType Type)[]
                {
                    (nameof(Neo.Network.P2P.Payloads.Transaction.Hash), PrimitiveContractType.Hash256),
                    (nameof(Neo.Network.P2P.Payloads.Transaction.Version), PrimitiveContractType.Integer),
                    (nameof(Neo.Network.P2P.Payloads.Transaction.Nonce), PrimitiveContractType.Integer),
                    (nameof(Neo.Network.P2P.Payloads.Transaction.Sender), PrimitiveContractType.Address),
                    (nameof(Neo.Network.P2P.Payloads.Transaction.SystemFee), PrimitiveContractType.Integer),
                    (nameof(Neo.Network.P2P.Payloads.Transaction.NetworkFee), PrimitiveContractType.Integer),
                    (nameof(Neo.Network.P2P.Payloads.Transaction.ValidUntilBlock), PrimitiveContractType.Integer),
                    (nameof(Neo.Network.P2P.Payloads.Transaction.Script), PrimitiveContractType.ByteArray),
                }));

        public static StructContractType TrimmedBlock => trimmedBlock.Value;
        static readonly Lazy<StructContractType> trimmedBlock = new(
            () => new StructContractType(
                nameof(TrimmedBlock),
                new (string Name, ContractType Type)[]
                {
                    (nameof(Neo.SmartContract.Native.TrimmedBlock.Header.Hash), PrimitiveContractType.Hash256),
                    (nameof(Neo.SmartContract.Native.TrimmedBlock.Header.Version), PrimitiveContractType.Integer),
                    (nameof(Neo.SmartContract.Native.TrimmedBlock.Header.PrevHash), PrimitiveContractType.Hash256),
                    (nameof(Neo.SmartContract.Native.TrimmedBlock.Header.MerkleRoot), PrimitiveContractType.Hash256),
                    (nameof(Neo.SmartContract.Native.TrimmedBlock.Header.Timestamp), PrimitiveContractType.Integer),
                    (nameof(Neo.SmartContract.Native.TrimmedBlock.Header.Nonce), PrimitiveContractType.Integer),
                    (nameof(Neo.SmartContract.Native.TrimmedBlock.Header.Index), PrimitiveContractType.Integer),
                    (nameof(Neo.SmartContract.Native.TrimmedBlock.Header.PrimaryIndex), PrimitiveContractType.Integer),
                    (nameof(Neo.SmartContract.Native.TrimmedBlock.Header.NextConsensus), PrimitiveContractType.Address),
                    (nameof(Neo.SmartContract.Native.TrimmedBlock.Hashes.Length), PrimitiveContractType.Integer),
                }));
   }
}