using System;
using Neo.BlockchainToolkit.Models;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;

namespace Neo.BlockchainToolkit.SmartContract
{
    public static class Extensions
    {
        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, DataCache snapshot)
            => new TestApplicationEngine(snapshot, chain.GetProtocolSettings());

        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, DataCache snapshot, UInt160 signer, WitnessScope witnessScope = WitnessScope.CalledByEntry)
            => new TestApplicationEngine(snapshot, chain.GetProtocolSettings(), signer, witnessScope);

        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, DataCache snapshot, Transaction transaction)
            => new TestApplicationEngine(snapshot, chain.GetProtocolSettings(), transaction);

        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, TriggerType trigger, IVerifiable? container, DataCache snapshot, Block? persistingBlock, long gas, Func<byte[], bool>? witnessChecker)
            => new TestApplicationEngine(trigger, container, snapshot, persistingBlock, chain.GetProtocolSettings(), gas, witnessChecker);
    }
}
