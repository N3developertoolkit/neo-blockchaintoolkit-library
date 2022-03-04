
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the scfx-gen tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Neo.BlockchainToolkit.Models;

public static class NeoCoreTypes
{
    public static StructContractType Block => _block.Value;
    static readonly Lazy<StructContractType> _block = new(() => 
        new StructContractType(
            "Neo#Block",
            new (string, ContractType)[] {
                ("Hash", PrimitiveContractType.Hash256),
                ("Version", PrimitiveContractType.Integer),
                ("PrevHash", PrimitiveContractType.Hash256),
                ("MerkleRoot", PrimitiveContractType.Hash256),
                ("Timestamp", PrimitiveContractType.Integer),
                ("Nonce", PrimitiveContractType.Integer),
                ("Index", PrimitiveContractType.Integer),
                ("PrimaryIndex", PrimitiveContractType.Integer),
                ("NextConsensus", PrimitiveContractType.Address),
                ("TransactionsCount", PrimitiveContractType.Integer),
            }));

    public static StructContractType Contract => _contract.Value;
    static readonly Lazy<StructContractType> _contract = new(() => 
        new StructContractType(
            "Neo#Contract",
            new (string, ContractType)[] {
                ("Id", PrimitiveContractType.Integer),
                ("UpdateCounter", PrimitiveContractType.Integer),
                ("Hash", PrimitiveContractType.Hash160),
                ("Nef", PrimitiveContractType.ByteArray),
                ("Manifest", NeoCoreTypes.ContractManifest),
            }));

    public static StructContractType ContractAbi => _contractAbi.Value;
    static readonly Lazy<StructContractType> _contractAbi = new(() => 
        new StructContractType(
            "Neo#ContractAbi",
            new (string, ContractType)[] {
                ("Methods", new ArrayContractType(NeoCoreTypes.ContractMethodDescriptor)),
                ("Events", new ArrayContractType(NeoCoreTypes.ContractEventDescriptor)),
            }));

    public static StructContractType ContractEventDescriptor => _contractEventDescriptor.Value;
    static readonly Lazy<StructContractType> _contractEventDescriptor = new(() => 
        new StructContractType(
            "Neo#ContractEventDescriptor",
            new (string, ContractType)[] {
                ("Name", PrimitiveContractType.String),
                ("Parameters", new ArrayContractType(NeoCoreTypes.ContractParameterDefinition)),
            }));

    public static StructContractType ContractGroup => _contractGroup.Value;
    static readonly Lazy<StructContractType> _contractGroup = new(() => 
        new StructContractType(
            "Neo#ContractGroup",
            new (string, ContractType)[] {
                ("PubKey", PrimitiveContractType.PublicKey),
                ("Signature", PrimitiveContractType.ByteArray),
            }));

    public static StructContractType ContractManifest => _contractManifest.Value;
    static readonly Lazy<StructContractType> _contractManifest = new(() => 
        new StructContractType(
            "Neo#ContractManifest",
            new (string, ContractType)[] {
                ("Name", PrimitiveContractType.String),
                ("Groups", new ArrayContractType(NeoCoreTypes.ContractGroup)),
                ("Reserved", UnspecifiedContractType.Unspecified),
                ("SupportedStandards", new ArrayContractType(PrimitiveContractType.String)),
                ("Abi", NeoCoreTypes.ContractAbi),
                ("Permissions", new ArrayContractType(NeoCoreTypes.ContractPermission)),
                ("Trusts", new ArrayContractType(PrimitiveContractType.ByteArray)),
                ("Extra", PrimitiveContractType.String),
            }));

    public static StructContractType ContractMethodDescriptor => _contractMethodDescriptor.Value;
    static readonly Lazy<StructContractType> _contractMethodDescriptor = new(() => 
        new StructContractType(
            "Neo#ContractMethodDescriptor",
            new (string, ContractType)[] {
                ("Name", PrimitiveContractType.String),
                ("Parameters", new ArrayContractType(NeoCoreTypes.ContractParameterDefinition)),
                ("ReturnType", PrimitiveContractType.Integer),
                ("Offset", PrimitiveContractType.Integer),
                ("Safe", PrimitiveContractType.Boolean),
            }));

    public static StructContractType ContractParameterDefinition => _contractParameterDefinition.Value;
    static readonly Lazy<StructContractType> _contractParameterDefinition = new(() => 
        new StructContractType(
            "Neo#ContractParameterDefinition",
            new (string, ContractType)[] {
                ("Name", PrimitiveContractType.String),
                ("Type", PrimitiveContractType.Integer),
            }));

    public static StructContractType ContractPermission => _contractPermission.Value;
    static readonly Lazy<StructContractType> _contractPermission = new(() => 
        new StructContractType(
            "Neo#ContractPermission",
            new (string, ContractType)[] {
                ("Contract", PrimitiveContractType.ByteArray),
                ("Methods", new ArrayContractType(PrimitiveContractType.String)),
            }));

    public static StructContractType NeoAccountState => _neoAccountState.Value;
    static readonly Lazy<StructContractType> _neoAccountState = new(() => 
        new StructContractType(
            "Neo#NeoAccountState",
            new (string, ContractType)[] {
                ("Balance", PrimitiveContractType.Integer),
                ("Height", PrimitiveContractType.Integer),
                ("VoteTo", PrimitiveContractType.PublicKey),
            }));

    public static StructContractType Nep11TokenState => _nep11TokenState.Value;
    static readonly Lazy<StructContractType> _nep11TokenState = new(() => 
        new StructContractType(
            "Neo#Nep11TokenState",
            new (string, ContractType)[] {
                ("Owner", PrimitiveContractType.Address),
                ("Name", PrimitiveContractType.String),
            }));

    public static StructContractType Notification => _notification.Value;
    static readonly Lazy<StructContractType> _notification = new(() => 
        new StructContractType(
            "Neo#Notification",
            new (string, ContractType)[] {
                ("ScriptHash", PrimitiveContractType.Hash160),
                ("EventName", PrimitiveContractType.String),
                ("State", new ArrayContractType(UnspecifiedContractType.Unspecified)),
            }));

    public static StructContractType StorageMap => _storageMap.Value;
    static readonly Lazy<StructContractType> _storageMap = new(() => 
        new StructContractType(
            "Neo#StorageMap",
            new (string, ContractType)[] {
                ("Context", new InteropContractType("StorageContext")),
                ("Prefix", PrimitiveContractType.ByteArray),
            }));

    public static StructContractType Transaction => _transaction.Value;
    static readonly Lazy<StructContractType> _transaction = new(() => 
        new StructContractType(
            "Neo#Transaction",
            new (string, ContractType)[] {
                ("Hash", PrimitiveContractType.Hash256),
                ("Version", PrimitiveContractType.Integer),
                ("Nonce", PrimitiveContractType.Integer),
                ("Sender", PrimitiveContractType.Address),
                ("SystemFee", PrimitiveContractType.Integer),
                ("NetworkFee", PrimitiveContractType.Integer),
                ("ValidUntilBlock", PrimitiveContractType.Integer),
                ("Script", PrimitiveContractType.ByteArray),
            }));

    static IReadOnlyDictionary<string, Lazy<StructContractType>> coreTypes 
        = new Dictionary<string, Lazy<StructContractType>>()
        {
            { "Neo#Block", _block },
            { "Neo#Contract", _contract },
            { "Neo#ContractAbi", _contractAbi },
            { "Neo#ContractEventDescriptor", _contractEventDescriptor },
            { "Neo#ContractGroup", _contractGroup },
            { "Neo#ContractManifest", _contractManifest },
            { "Neo#ContractMethodDescriptor", _contractMethodDescriptor },
            { "Neo#ContractParameterDefinition", _contractParameterDefinition },
            { "Neo#ContractPermission", _contractPermission },
            { "Neo#NeoAccountState", _neoAccountState },
            { "Neo#Nep11TokenState", _nep11TokenState },
            { "Neo#Notification", _notification },
            { "Neo#StorageMap", _storageMap },
            { "Neo#Transaction", _transaction },
        };

    public static bool TryGetType(string name, [MaybeNullWhen(false)] out StructContractType type)
    {
        if (coreTypes.TryGetValue(name, out var lazy))
        {
            type = lazy.Value;
            return true;
        }

        type = default;
        return false;
    }
}
