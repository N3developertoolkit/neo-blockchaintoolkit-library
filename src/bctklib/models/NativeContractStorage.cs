using System;
using System.Collections.Generic;

namespace Neo.BlockchainToolkit.Models;

public static class NativeContractStorage
{
    public static IReadOnlyList<StorageDef> ContractManagement => _contractManagement.Value;
    static readonly Lazy<IReadOnlyList<StorageDef>> _contractManagement = new(() => new[]
    {
        new StorageDef("Contract", 8, NativeStructs.Contract,
            new KeySegment("hash", PrimitiveType.Hash160)),
        new StorageDef("NextAvailableId", 15, PrimitiveContractType.Integer),
        new StorageDef("MinimumDeploymentFee", 20, PrimitiveContractType.Integer),
    });

    public static IReadOnlyList<StorageDef> GasToken => _gasToken.Value;
    static readonly Lazy<IReadOnlyList<StorageDef>> _gasToken = new(() => new[]
    {
        new StorageDef("TotalSupply", 11, PrimitiveContractType.Integer),
        new StorageDef("Account", 20, 
            new StructContractType(
                "Neo#GasAccountState",
                new (string, ContractType)[] {
                    ("Balance", PrimitiveContractType.Integer)
                }),
            new KeySegment("account", PrimitiveType.Hash160)),
    });

    public static IReadOnlyList<StorageDef> LedgerContract => _ledgerContract.Value;
    static readonly Lazy<IReadOnlyList<StorageDef>> _ledgerContract = new(() => new[]
    {
        new StorageDef("BlockHash", 9, PrimitiveContractType.Hash256,
            new KeySegment("index", default(BlockIndex))),
        new StorageDef("CurrentBlock", 12, 
            new StructContractType(
                "Neo#HashIndexState",
                new (string, ContractType)[] {
                    ("Hash", PrimitiveContractType.Hash256),
                    ("Index", PrimitiveContractType.Integer)
                })),
        new StorageDef("Block", 5, NativeStructs.Block,
            new KeySegment("hash", PrimitiveType.Hash256)),
        new StorageDef("Transaction", 11, NativeStructs.Transaction,
            new KeySegment("hash", PrimitiveType.Hash256)),
    });

    public static IReadOnlyList<StorageDef> NeoToken => _neoToken.Value;
    static readonly Lazy<IReadOnlyList<StorageDef>> _neoToken = new(() => new[]
    {
        new StorageDef("TotalSupply", 11, PrimitiveContractType.Integer),
        new StorageDef("Account", 20, NativeStructs.NeoAccountState,
            new KeySegment("account", PrimitiveType.Hash160)),
        new StorageDef("VotersCount", 1, PrimitiveContractType.Integer),
        new StorageDef("Candidate", 33, 
            new StructContractType(
                "Neo#CandidateState",
                new (string, ContractType)[] {
                    ("Registered", PrimitiveContractType.Boolean),
                    ("Votes", PrimitiveContractType.Integer)
                }),
            new KeySegment("candidate", PrimitiveType.PublicKey)),
        new StorageDef("Committee", 14, new ArrayContractType(
            new StructContractType(
                "Neo#CachedCommittee",
                new (string, ContractType)[] {
                    ("PublicKey", PrimitiveContractType.PublicKey),
                    ("Votes", PrimitiveContractType.Integer)
                }))),
        new StorageDef("GasPerBlock", 29, PrimitiveContractType.Integer,
            new KeySegment("index", default(BlockIndex))),
        new StorageDef("RegisterPrice", 13, PrimitiveContractType.Integer),
        new StorageDef("VoterRewardPerCommittee", 23, PrimitiveContractType.Integer,
            new KeySegment("publicKey", PrimitiveType.PublicKey),
            new KeySegment("index", default(BlockIndex))),
    });

    public static IReadOnlyList<StorageDef> OracleContract => _oracleContract.Value;
    static readonly Lazy<IReadOnlyList<StorageDef>> _oracleContract = new(() => new[]
    {
        new StorageDef("Price", 5, PrimitiveContractType.Integer),
        new StorageDef("RequestId", 9, PrimitiveContractType.Integer),
        new StorageDef("Request", 7,
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
            new KeySegment("index", default(BlockIndex))),
        new StorageDef("IdList", 9, new ArrayContractType(PrimitiveContractType.Integer),
            new KeySegment("urlHash", PrimitiveType.Hash160)),
    });

    public static IReadOnlyList<StorageDef> PolicyContract => _policyContract.Value;
    static readonly Lazy<IReadOnlyList<StorageDef>> _policyContract = new(() => new[]
    {
        new StorageDef("BlockedAccount", 15, PrimitiveContractType.ByteArray,
            new KeySegment("account", PrimitiveType.Address)),
        new StorageDef("FeePerByte", 10, PrimitiveContractType.Integer),
        new StorageDef("ExecFeeFactor", 18, PrimitiveContractType.Integer),
        new StorageDef("StoragePrice", 19, PrimitiveContractType.Integer),
    });

    public static IReadOnlyList<StorageDef> RoleManagement => _roleManagement.Value;
    static readonly Lazy<IReadOnlyList<StorageDef>> _roleManagement = new(() => new[]
    {
        new StorageDef("StateValidator", 4, NodeList,
            new KeySegment("index", default(BlockIndex))), 
        new StorageDef("Oracle", 8, NodeList,
            new KeySegment("index", default(BlockIndex))),
        new StorageDef("NeoFSAlphabetNode", 16, NodeList,
            new KeySegment("index", default(BlockIndex))),
    });

    public static StructContractType NodeList => _NodeList.Value;
    static readonly Lazy<StructContractType> _NodeList = new(() => 
        new StructContractType(
            "Neo#NodeList",
            new (string, ContractType)[] {
                ("Nodes", new ArrayContractType(PrimitiveContractType.PublicKey)),
            }));

}
