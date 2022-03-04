
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;

namespace Neo.BlockchainToolkit.Models;

public static class NeoCoreTypes
{
    public static StructContractType Nep11TokenState => _nep11TokenState.Value;
    static readonly Lazy<StructContractType> _nep11TokenState = new(
        () => new StructContractType(
            "Nep11TokenState",
            new (string, ContractType)[] {
                ("Owner", PrimitiveContractType.Hash160),
                ("Name", PrimitiveContractType.String),
            }));

    public static StructContractType NeoAccountState => _neoAccountState.Value;
    static readonly Lazy<StructContractType> _neoAccountState = new(
        () => new StructContractType(
            "NeoAccountState",
            new (string, ContractType)[] {
                ("Balance", PrimitiveContractType.Integer),
                ("Height", PrimitiveContractType.Integer),
                ("VoteTo", PrimitiveContractType.PublicKey),
            }));

    public static StructContractType Block => _block.Value;
    static readonly Lazy<StructContractType> _block = new(
        () => new StructContractType(
            "Block",
            new (string, ContractType)[] {
                ("Hash", PrimitiveContractType.Hash256),
                ("Version", PrimitiveContractType.Integer),
                ("PrevHash", PrimitiveContractType.Hash256),
                ("MerkleRoot", PrimitiveContractType.Hash256),
                ("Timestamp", PrimitiveContractType.Integer),
                ("Nonce", PrimitiveContractType.Integer),
                ("Index", PrimitiveContractType.Integer),
                ("PrimaryIndex", PrimitiveContractType.Integer),
                ("NextConsensus", PrimitiveContractType.Hash160),
                ("TransactionsCount", PrimitiveContractType.Integer),
            }));

    public static StructContractType Contract => _contract.Value;
    static readonly Lazy<StructContractType> _contract = new(
        () => new StructContractType(
            "Contract",
            new (string, ContractType)[] {
                ("Id", PrimitiveContractType.Integer),
                ("UpdateCounter", PrimitiveContractType.Integer),
                ("Hash", PrimitiveContractType.Hash160),
                ("Nef", PrimitiveContractType.ByteArray),
                ("Manifest", ContractManifest),
            }));

    public static StructContractType ContractAbi => _contractAbi.Value;
    static readonly Lazy<StructContractType> _contractAbi = new(
        () => new StructContractType(
            "ContractAbi",
            new (string, ContractType)[] {
                ("Methods", new ArrayContractType(ContractMethodDescriptor)),
                ("Events", new ArrayContractType(ContractEventDescriptor)),
            }));

    public static StructContractType ContractEventDescriptor => _contractEventDescriptor.Value;
    static readonly Lazy<StructContractType> _contractEventDescriptor = new(
        () => new StructContractType(
            "ContractEventDescriptor",
            new (string, ContractType)[] {
                ("Name", PrimitiveContractType.String),
                ("Parameters", new ArrayContractType(ContractParameterDefinition)),
            }));

    public static StructContractType ContractGroup => _contractGroup.Value;
    static readonly Lazy<StructContractType> _contractGroup = new(
        () => new StructContractType(
            "ContractGroup",
            new (string, ContractType)[] {
                ("PubKey", PrimitiveContractType.PublicKey),
                ("Signature", PrimitiveContractType.ByteArray),
            }));

    public static StructContractType ContractManifest => _contractManifest.Value;
    static readonly Lazy<StructContractType> _contractManifest = new(
        () => new StructContractType(
            "ContractManifest",
            new (string, ContractType)[] {
                ("Name", PrimitiveContractType.String),
                ("Groups", new ArrayContractType(ContractGroup)),
                ("Reserved", UnspecifiedContractType.Unspecified),
                ("SupportedStandards", new ArrayContractType(PrimitiveContractType.String)),
                ("Abi", ContractAbi),
                ("Permissions", new ArrayContractType(ContractPermission)),
                ("Trusts", new ArrayContractType(PrimitiveContractType.ByteArray)),
                ("Extra", PrimitiveContractType.String),
            }));

    public static StructContractType ContractMethodDescriptor => _contractMethodDescriptor.Value;
    static readonly Lazy<StructContractType> _contractMethodDescriptor = new(
        () => new StructContractType(
            "ContractMethodDescriptor",
            new (string, ContractType)[] {
                ("Name", PrimitiveContractType.String),
                ("Parameters", new ArrayContractType(ContractParameterDefinition)),
                ("ReturnType", PrimitiveContractType.Integer),
                ("Offset", PrimitiveContractType.Integer),
                ("Safe", PrimitiveContractType.Boolean),
            }));

    public static StructContractType ContractParameterDefinition => _contractParameterDefinition.Value;
    static readonly Lazy<StructContractType> _contractParameterDefinition = new(
        () => new StructContractType(
            "ContractParameterDefinition",
            new (string, ContractType)[] {
                ("Name", PrimitiveContractType.String),
                ("Type", PrimitiveContractType.Integer),
            }));

    public static StructContractType ContractPermission => _contractPermission.Value;
    static readonly Lazy<StructContractType> _contractPermission = new(
        () => new StructContractType(
            "ContractPermission",
            new (string, ContractType)[] {
                ("Contract", PrimitiveContractType.ByteArray),
                ("Methods", new ArrayContractType(PrimitiveContractType.String)),
            }));

    public static StructContractType Notification => _notification.Value;
    static readonly Lazy<StructContractType> _notification = new(
        () => new StructContractType(
            "Notification",
            new (string, ContractType)[] {
                ("ScriptHash", PrimitiveContractType.Hash160),
                ("EventName", PrimitiveContractType.String),
                ("State", new ArrayContractType(UnspecifiedContractType.Unspecified)),
            }));

    public static StructContractType StorageMap => _storageMap.Value;
    static readonly Lazy<StructContractType> _storageMap = new(
        () => new StructContractType(
            "StorageMap",
            new (string, ContractType)[] {
                ("Context", new InteropContractType("StorageContext")),
                ("Prefix", PrimitiveContractType.ByteArray),
            }));

    public static StructContractType Transaction => _transaction.Value;
    static readonly Lazy<StructContractType> _transaction = new(
        () => new StructContractType(
            "Transaction",
            new (string, ContractType)[] {
                ("Hash", PrimitiveContractType.Hash256),
                ("Version", PrimitiveContractType.Integer),
                ("Nonce", PrimitiveContractType.Integer),
                ("Sender", PrimitiveContractType.Hash160),
                ("SystemFee", PrimitiveContractType.Integer),
                ("NetworkFee", PrimitiveContractType.Integer),
                ("ValidUntilBlock", PrimitiveContractType.Integer),
                ("Script", PrimitiveContractType.ByteArray),
            }));
}
