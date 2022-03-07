using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Neo.SmartContract.Native;

namespace Neo.BlockchainToolkit.Models;

public static class NativeContractStorage
{
    static readonly IReadOnlyList<StorageDef> contractManagement = new []
    {
        new StorageDef("Contract", 8, NativeStructs.Contract,
            new KeySegment("hash", PrimitiveType.Hash160)),
        new StorageDef("NextAvailableId", 15, PrimitiveContractType.Integer),
        new StorageDef("MinimumDeploymentFee", 20, PrimitiveContractType.Integer),
    };

    static readonly IReadOnlyList<StorageDef> gasToken = new []
    {
        new StorageDef("TotalSupply", 11, PrimitiveContractType.Integer),
        new StorageDef("Account", 20, 
            new StructContractType(
                "Neo#GasAccountState",
                new (string, ContractType)[] {
                    ("Balance", PrimitiveContractType.Integer)
                }),
            new KeySegment("account", PrimitiveType.Hash160)),
    };

    static readonly IReadOnlyList<StorageDef> ledgerContract = new []
    {
        new StorageDef("BlockHash", 9, PrimitiveContractType.Hash256,
            new KeySegment("index", (PrimitiveType)0xff)), // big endian uint
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
    };

    static readonly IReadOnlyList<StorageDef> neoToken = new []
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
            new KeySegment("index", PrimitiveType.Integer)), // big endian uint
        new StorageDef("RegisterPrice", 13, PrimitiveContractType.Integer),
        new StorageDef("VoterRewardPerCommittee", 23, PrimitiveContractType.Integer,
            new KeySegment("publicKey", PrimitiveType.PublicKey),
            new KeySegment("index", PrimitiveType.Integer)), // big endian uint
    };

    static readonly IReadOnlyList<StorageDef> oracleContract = new []
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
            new KeySegment("index", PrimitiveType.Integer)), // big endian uint
        new StorageDef("IdList", 9, new ArrayContractType(PrimitiveContractType.Integer),
            new KeySegment("urlHash", PrimitiveType.Hash160)),
    };

    // PolicyContract
    // RoleManagement
}
