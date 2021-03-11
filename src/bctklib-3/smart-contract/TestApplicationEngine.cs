using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Neo.Cryptography;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;

namespace Neo.BlockchainToolkit.SmartContract
{
    using WitnessChecker = Func<byte[], bool>;

    public partial class TestApplicationEngine : ApplicationEngine
    {
        private readonly static IReadOnlyDictionary<uint, InteropDescriptor> overriddenServices;

        static TestApplicationEngine()
        {
            var builder = ImmutableDictionary.CreateBuilder<uint, InteropDescriptor>();

            AddOverload(builder, "System.Runtime.CheckWitness", nameof(CheckWitnessOverride));
 
            overriddenServices = builder.ToImmutable();

            static void AddOverload(ImmutableDictionary<uint, InteropDescriptor>.Builder builder, string sysCallName, string overloadedMethodName)
            {
                var sysCallHash = BinaryPrimitives.ReadUInt32LittleEndian(Encoding.ASCII.GetBytes(sysCallName).Sha256());
                var handler = typeof(TestApplicationEngine).GetMethod(overloadedMethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException();
                var descriptor = ApplicationEngine.Services[sysCallHash] with { Handler = handler };

                builder.Add(sysCallHash, descriptor);
            }
        }

        private readonly WitnessChecker witnessChecker;

        public TestApplicationEngine(DataCache snapshot, ProtocolSettings settings) : this(TriggerType.Application, null, snapshot, null, settings, ApplicationEngine.TestModeGas, null)
        {
        }

        public TestApplicationEngine(DataCache snapshot, ProtocolSettings settings, UInt160 signer) : this(TriggerType.Application, new TestVerifiable(signer), snapshot, null, settings, ApplicationEngine.TestModeGas, null)
        {
        }

        public TestApplicationEngine(TriggerType trigger, IVerifiable? container, DataCache snapshot, Block? persistingBlock, ProtocolSettings settings, long gas, WitnessChecker? witnessChecker)
            : base(trigger, container ?? new TestVerifiable(), snapshot, persistingBlock, settings, gas)
        {
            this.witnessChecker = witnessChecker ?? CheckWitness;
            ApplicationEngine.Log += OnLog;
            ApplicationEngine.Notify += OnNotify;
        }

        public override void Dispose()
        {
            ApplicationEngine.Log -= OnLog;
            ApplicationEngine.Notify -= OnNotify;
            base.Dispose();
        }

        public new event EventHandler<LogEventArgs>? Log;
        public new event EventHandler<NotifyEventArgs>? Notify;

        private void OnLog(object? sender, LogEventArgs args)
        {
            if (ReferenceEquals(this, sender))
            {
                this.Log?.Invoke(sender, args);
            }
        }

        private void OnNotify(object? sender, NotifyEventArgs args)
        {
            if (ReferenceEquals(this, sender))
            {
                this.Notify?.Invoke(sender, args);
            }
        }

        protected internal bool CheckWitnessOverride(byte[] hashOrPubkey) => witnessChecker.Invoke(hashOrPubkey);

        protected override void OnSysCall(uint methodHash)
        {
            if (overriddenServices.TryGetValue(methodHash, out var descriptor))
            {
                base.OnSysCall(descriptor);
            }
            else
            {
                base.OnSysCall(methodHash);
            }
        }
    }
}
