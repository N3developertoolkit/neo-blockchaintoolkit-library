using System;
using System.Collections.Generic;

namespace Neo.BlockchainToolkit.Models;

public static class NativeContractStorage
{
    public static IReadOnlyList<StorageGroup> ContractManagement => _contractManagement.Value;
    static readonly Lazy<IReadOnlyList<StorageGroup>> _contractManagement = new(() => new[]
    {
        new StorageGroup("Contract", 8, NativeStructs.Contract,
            new KeySegment("hash", PrimitiveType.Hash160)),
        new StorageGroup("NextAvailableId", 15, PrimitiveContractType.Integer),
        new StorageGroup("MinimumDeploymentFee", 20, PrimitiveContractType.Integer),
    });

    public static IReadOnlyList<StorageGroup> GasToken => _gasToken.Value;
    static readonly Lazy<IReadOnlyList<StorageGroup>> _gasToken = new(() => new[]
    {
        new StorageGroup("TotalSupply", 11, PrimitiveContractType.Integer),
        new StorageGroup("Account", 20, 
            new StructContractType(
                "Neo#GasAccountState",
                new (string, ContractType)[] {
                    ("Balance", PrimitiveContractType.Integer)
                }),
            new KeySegment("account", PrimitiveType.Hash160)),
    });

    public static IReadOnlyList<StorageGroup> LedgerContract => _ledgerContract.Value;
    static readonly Lazy<IReadOnlyList<StorageGroup>> _ledgerContract = new(() => new[]
    {
        new StorageGroup("BlockHash", 9, PrimitiveContractType.Hash256,
            new KeySegment("index", PrimitiveType.Integer)), // block indewx
        new StorageGroup("CurrentBlock", 12, 
            new StructContractType(
                "Neo#HashIndexState",
                new (string, ContractType)[] {
                    ("Hash", PrimitiveContractType.Hash256),
                    ("Index", PrimitiveContractType.Integer)
                })),
        new StorageGroup("Block", 5, NativeStructs.Block,
            new KeySegment("hash", PrimitiveType.Hash256)),
        new StorageGroup("Transaction", 11, NativeStructs.Transaction,
            new KeySegment("hash", PrimitiveType.Hash256)),
    });

    public static IReadOnlyList<StorageGroup> NeoToken => _neoToken.Value;
    static readonly Lazy<IReadOnlyList<StorageGroup>> _neoToken = new(() => new[]
    {
        new StorageGroup("TotalSupply", 11, PrimitiveContractType.Integer),
        new StorageGroup("Account", 20, NativeStructs.NeoAccountState,
            new KeySegment("account", PrimitiveType.Hash160)),
        new StorageGroup("VotersCount", 1, PrimitiveContractType.Integer),
        new StorageGroup("Candidate", 33, 
            new StructContractType(
                "Neo#CandidateState",
                new (string, ContractType)[] {
                    ("Registered", PrimitiveContractType.Boolean),
                    ("Votes", PrimitiveContractType.Integer)
                }),
            new KeySegment("candidate", PrimitiveType.PublicKey)),
        new StorageGroup("Committee", 14, new ArrayContractType(
            new StructContractType(
                "Neo#CachedCommittee",
                new (string, ContractType)[] {
                    ("PublicKey", PrimitiveContractType.PublicKey),
                    ("Votes", PrimitiveContractType.Integer)
                }))),
        new StorageGroup("GasPerBlock", 29, PrimitiveContractType.Integer,
            new KeySegment("index", PrimitiveType.Integer)), // block indewx
        new StorageGroup("RegisterPrice", 13, PrimitiveContractType.Integer),
        new StorageGroup("VoterRewardPerCommittee", 23, PrimitiveContractType.Integer,
            new KeySegment("publicKey", PrimitiveType.PublicKey),
            new KeySegment("index", PrimitiveType.Integer)), // block indewx
    });

    public static IReadOnlyList<StorageGroup> OracleContract => _oracleContract.Value;
    static readonly Lazy<IReadOnlyList<StorageGroup>> _oracleContract = new(() => new[]
    {
        new StorageGroup("Price", 5, PrimitiveContractType.Integer),
        new StorageGroup("RequestId", 9, PrimitiveContractType.Integer),
        new StorageGroup("Request", 7,
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
        new StorageGroup("IdList", 9, new ArrayContractType(PrimitiveContractType.Integer),
            new KeySegment("urlHash", PrimitiveType.Hash160)),
    });

    public static IReadOnlyList<StorageGroup> PolicyContract => _policyContract.Value;
    static readonly Lazy<IReadOnlyList<StorageGroup>> _policyContract = new(() => new[]
    {
        new StorageGroup("BlockedAccount", 15, PrimitiveContractType.ByteArray,
            new KeySegment("account", PrimitiveType.Address)),
        new StorageGroup("FeePerByte", 10, PrimitiveContractType.Integer),
        new StorageGroup("ExecFeeFactor", 18, PrimitiveContractType.Integer),
        new StorageGroup("StoragePrice", 19, PrimitiveContractType.Integer),
    });

    public static IReadOnlyList<StorageGroup> RoleManagement => _roleManagement.Value;
    static readonly Lazy<IReadOnlyList<StorageGroup>> _roleManagement = new(() => new[]
    {
        new StorageGroup("StateValidator", 4, NodeList,
            new KeySegment("index", PrimitiveType.Integer)), // block indewx
        new StorageGroup("Oracle", 8, NodeList,
            new KeySegment("index", PrimitiveType.Integer)), // block indewx
        new StorageGroup("NeoFSAlphabetNode", 16, NodeList,
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
