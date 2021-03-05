using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Neo.Cryptography;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;

namespace Neo.BlockchainToolkit.SmartContract
{
    using WitnessChecker = Func<byte[], bool>;

    public partial class TestApplicationEngine : ApplicationEngine
    {
        private readonly static IReadOnlyDictionary<uint, MethodInfo> overriddenServices;

        static TestApplicationEngine()
        {
            var builder = ImmutableDictionary.CreateBuilder<uint, MethodInfo>();
            builder.Add(HashMethodName("System.Runtime.CheckWitness"), GetMethodInfo(nameof(CheckWitnessOverride)));
            overriddenServices = builder.ToImmutable();

            static MethodInfo GetMethodInfo(string handler)
            {
                return typeof(TestApplicationEngine).GetMethod(handler, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException();
            }

            static uint HashMethodName(string name)
            {
                return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(System.Text.Encoding.ASCII.GetBytes(name).Sha256());
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

        // remove when https://github.com/neo-project/neo/pull/2378 is merged
        protected void OnSysCall(InteropDescriptor descriptor, Func<object, object[], object> handler)
        {
            var exec_fee_factor_prop = this.GetType().GetProperty("exec_fee_factor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                 ?? throw new InvalidOperationException();
            var exec_fee_factor = (uint)(exec_fee_factor_prop.GetValue(this) ?? throw new InvalidOperationException());
            ValidateCallFlags(descriptor.RequiredCallFlags);
            AddGas(descriptor.FixedPrice * exec_fee_factor);

            object[] parameters = descriptor.Parameters.Count > 0
                 ? new object[descriptor.Parameters.Count] 
                 : Array.Empty<object>();

            for (int i = 0; i < descriptor.Parameters.Count; i++)
            {
                parameters[i] = Convert(Pop(), descriptor.Parameters[i]);
            }

            object returnValue = handler(this, parameters);
            if (descriptor.Handler.ReturnType != typeof(void))
            {
                Push(Convert(returnValue));
            }
        }

        protected override void OnSysCall(uint methodHash)
        {
            if (overriddenServices.TryGetValue(methodHash, out var method))
            {
                InteropDescriptor descriptor = Services[methodHash];
                OnSysCall(descriptor, method.Invoke!);
            }
            else
            {
                base.OnSysCall(methodHash);
            }
        }
    }
}
