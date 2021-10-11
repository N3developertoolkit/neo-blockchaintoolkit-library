using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace Neo.BlockchainToolkit.SmartContract
{
    public class TraceApplicationEngine : ApplicationEngine
    {
        readonly ITraceDebugSink traceDebugSink;
        ImmutableDictionary<UInt160, string> contractNameMap = ImmutableDictionary<UInt160, string>.Empty;

        public TraceApplicationEngine(ITraceDebugSink traceDebugSink, TriggerType trigger, IVerifiable container,
                                      DataCache snapshot, Block? persistingBlock, ProtocolSettings settings, long gas)
            : base(trigger, container, snapshot, persistingBlock, settings, gas)
        {
            this.traceDebugSink = traceDebugSink;

            Log += OnLog!;
            Notify += OnNotify!;
        }

        public override void Dispose()
        {
            Log -= OnLog!;
            Notify -= OnNotify!;
            traceDebugSink.Dispose();
            base.Dispose();
        }

        private string GetContractName(UInt160 scriptId)
        {
            return ImmutableInterlocked.GetOrAdd(ref contractNameMap, scriptId, 
                k => NativeContract.ContractManagement.GetContract(Snapshot, k)?.Manifest.Name ?? string.Empty);
        }

        private void OnNotify(object sender, NotifyEventArgs args)
        {
            if (ReferenceEquals(sender, this))
            {
                traceDebugSink.Notify(args, GetContractName(args.ScriptHash));
            }
        }

        private void OnLog(object sender, LogEventArgs args)
        {
            if (ReferenceEquals(sender, this))
            {
                traceDebugSink.Log(args, GetContractName(args.ScriptHash));
            }
        }

        public override VMState Execute()
        {
            traceDebugSink.ProtocolSettings(ProtocolSettings.Network, ProtocolSettings.AddressVersion);
            traceDebugSink.Script(CurrentContext?.Script ?? Array.Empty<byte>());
            traceDebugSink.Trace(State, GasConsumed, InvocationStack);
            WriteStorages(CurrentScriptHash);

            return base.Execute();
        }

        protected override void PostExecuteInstruction()
        {
            base.PostExecuteInstruction();

            if (State == VMState.HALT)
            {
                traceDebugSink.Results(State, GasConsumed, ResultStack);
            }
            traceDebugSink.Trace(State, GasConsumed, InvocationStack);
            WriteStorages(CurrentScriptHash);
        }

        protected override void OnFault(Exception e)
        {
            base.OnFault(e);
            traceDebugSink.Fault(e);
            traceDebugSink.Trace(State, GasConsumed, InvocationStack);
        }

        private void WriteStorages(UInt160 scriptHash)
        {
            if (scriptHash != null)
            {
                var contractState = NativeContract.ContractManagement.GetContract(Snapshot, scriptHash);
                if (contractState != null)
                {
                    var storages = Snapshot.Find(StorageKey.CreateSearchPrefix(contractState.Id, default));
                    traceDebugSink.Storages(scriptHash, storages);
                }
            }
        }
    }
}
