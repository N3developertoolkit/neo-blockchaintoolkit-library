using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.Wallets;

namespace Neo.BlockchainToolkit
{
    public static class ExpressChainExtensions
    {
        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, DataCache snapshot)
            => new TestApplicationEngine(snapshot, chain.ProtocolSettings);

        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, DataCache snapshot, UInt160 signer, WitnessScope witnessScope = WitnessScope.CalledByEntry)
            => new TestApplicationEngine(snapshot, chain.ProtocolSettings, signer, witnessScope);

        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, DataCache snapshot, Transaction transaction)
            => new TestApplicationEngine(snapshot, chain.ProtocolSettings, transaction);

        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, TriggerType trigger, IVerifiable? container, DataCache snapshot, Block? persistingBlock, long gas, Func<byte[], bool>? witnessChecker)
            => new TestApplicationEngine(trigger, container, snapshot, persistingBlock, chain.ProtocolSettings, gas, witnessChecker);
    }
}
