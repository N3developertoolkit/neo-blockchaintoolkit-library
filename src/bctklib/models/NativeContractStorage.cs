using System;
using System.Collections.Generic;

namespace Neo.BlockchainToolkit.Models;

public static class NativeContractStorage
{
    public static IReadOnlyList<StorageGroupDef> ContractManagement => _contractManagement.Value;
    static readonly Lazy<IReadOnlyList<StorageGroupDef>> _contractManagement = new(() => new[]
    {
        new StorageGroupDef("Contract", 8, NativeStructs.Contract,
            new KeySegment("hash", PrimitiveType.Hash160)),
        new StorageGroupDef("NextAvailableId", 15, PrimitiveContractType.Integer),
        new StorageGroupDef("MinimumDeploymentFee", 20, PrimitiveContractType.Integer),
    });

    public static IReadOnlyList<StorageGroupDef> GasToken => _gasToken.Value;
    static readonly Lazy<IReadOnlyList<StorageGroupDef>> _gasToken = new(() => new[]
    {
        new StorageGroupDef("TotalSupply", 11, PrimitiveContractType.Integer),
        new StorageGroupDef("Account", 20, 
            new StructContractType(
                "Neo#GasAccountState",
                new (string, ContractType)[] {
                    ("Balance", PrimitiveContractType.Integer)
                }),
            new KeySegment("account", PrimitiveType.Hash160)),
    });

    public static IReadOnlyList<StorageGroupDef> LedgerContract => _ledgerContract.Value;
    static readonly Lazy<IReadOnlyList<StorageGroupDef>> _ledgerContract = new(() => new[]
    {
        new StorageGroupDef("BlockHash", 9, PrimitiveContractType.Hash256,
            new KeySegment("index", PrimitiveType.Integer)), // block indewx
        new StorageGroupDef("CurrentBlock", 12, 
            new StructContractType(
                "Neo#HashIndexState",
                new (string, ContractType)[] {
                    ("Hash", PrimitiveContractType.Hash256),
                    ("Index", PrimitiveContractType.Integer)
                })),
        new StorageGroupDef("Block", 5, NativeStructs.Block,
            new KeySegment("hash", PrimitiveType.Hash256)),
        new StorageGroupDef("Transaction", 11, NativeStructs.Transaction,
            new KeySegment("hash", PrimitiveType.Hash256)),
    });

    public static IReadOnlyList<StorageGroupDef> NeoToken => _neoToken.Value;
    static readonly Lazy<IReadOnlyList<StorageGroupDef>> _neoToken = new(() => new[]
    {
        new StorageGroupDef("TotalSupply", 11, PrimitiveContractType.Integer),
        new StorageGroupDef("Account", 20, NativeStructs.NeoAccountState,
            new KeySegment("account", PrimitiveType.Hash160)),
        new StorageGroupDef("VotersCount", 1, PrimitiveContractType.Integer),
        new StorageGroupDef("Candidate", 33, 
            new StructContractType(
                "Neo#CandidateState",
                new (string, ContractType)[] {
                    ("Registered", PrimitiveContractType.Boolean),
                    ("Votes", PrimitiveContractType.Integer)
                }),
            new KeySegment("candidate", PrimitiveType.PublicKey)),
        new StorageGroupDef("Committee", 14, new ArrayContractType(
            new StructContractType(
                "Neo#CachedCommittee",
                new (string, ContractType)[] {
                    ("PublicKey", PrimitiveContractType.PublicKey),
                    ("Votes", PrimitiveContractType.Integer)
                }))),
        new StorageGroupDef("GasPerBlock", 29, PrimitiveContractType.Integer,
            new KeySegment("index", PrimitiveType.Integer)), // block indewx
        new StorageGroupDef("RegisterPrice", 13, PrimitiveContractType.Integer),
        new StorageGroupDef("VoterRewardPerCommittee", 23, PrimitiveContractType.Integer,
            new KeySegment("publicKey", PrimitiveType.PublicKey),
            new KeySegment("index", PrimitiveType.Integer)), // block indewx
    });

    public static IReadOnlyList<StorageGroupDef> OracleContract => _oracleContract.Value;
    static readonly Lazy<IReadOnlyList<StorageGroupDef>> _oracleContract = new(() => new[]
    {
        new StorageGroupDef("Price", 5, PrimitiveContractType.Integer),
        new StorageGroupDef("RequestId", 9, PrimitiveContractType.Integer),
        new StorageGroupDef("Request", 7,
            new StructContractType(
                "Neo#OracleRequest",
                new (string, ContractType)[] {
                    ("OriginalTxid", PrimitiveContractType.Hash256),
                    ("GasForResponse", PrimitiveContractType.Integer),
                    ("Url", PrimitiveContractType.String),
                    ("Filter", PrimitiveContractType.String),
                    ("CallbackContract", PrimitiveContractType.Hash160),
                    ("CallbackMethod", PrimitiveContractType.String),
                    ("UserData", PrimitiveContractType.ByteArray)
                }),
            new KeySegment("index", PrimitiveType.Integer)), // block indewx
        new StorageGroupDef("IdList", 9, new ArrayContractType(PrimitiveContractType.Integer),
            new KeySegment("urlHash", PrimitiveType.Hash160)),
    });

    public static IReadOnlyList<StorageGroupDef> PolicyContract => _policyContract.Value;
    static readonly Lazy<IReadOnlyList<StorageGroupDef>> _policyContract = new(() => new[]
    {
        new StorageGroupDef("BlockedAccount", 15, PrimitiveContractType.ByteArray,
            new KeySegment("account", PrimitiveType.Address)),
        new StorageGroupDef("FeePerByte", 10, PrimitiveContractType.Integer),
        new StorageGroupDef("ExecFeeFactor", 18, PrimitiveContractType.Integer),
        new StorageGroupDef("StoragePrice", 19, PrimitiveContractType.Integer),
    });

    public static IReadOnlyList<StorageGroupDef> RoleManagement => _roleManagement.Value;
    static readonly Lazy<IReadOnlyList<StorageGroupDef>> _roleManagement = new(() => new[]
    {
        new StorageGroupDef("StateValidator", 4, NodeList,
            new KeySegment("index", PrimitiveType.Integer)), // block indewx
        new StorageGroupDef("Oracle", 8, NodeList,
            new KeySegment("index", PrimitiveType.Integer)), // block indewx
        new StorageGroupDef("NeoFSAlphabetNode", 16, NodeList,
            new KeySegment("index", PrimitiveType.Integer)), // block indewx
    });

    public static StructContractType NodeList => _NodeList.Value;
    static readonly Lazy<StructContractType> _NodeList = new(() => 
        new StructContractType(
            "Neo#NodeList",
            new (string, ContractType)[] {
                ("Nodes", new ArrayContractType(PrimitiveContractType.PublicKey)),
            }));

}
