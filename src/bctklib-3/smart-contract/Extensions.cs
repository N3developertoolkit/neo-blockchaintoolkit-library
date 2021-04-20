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

        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, DataCache snapshot, UInt160 signer)
            => new TestApplicationEngine(snapshot, chain.GetProtocolSettings(), signer);

        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, TriggerType trigger, IVerifiable? container, DataCache snapshot, Block? persistingBlock, long gas, Func<byte[], bool>? witnessChecker)
            => new TestApplicationEngine(trigger, container, snapshot, persistingBlock, chain.GetProtocolSettings(), gas, witnessChecker);
    }
}
