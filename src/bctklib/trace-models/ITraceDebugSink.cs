using System;
using System.Collections.Generic;
using Neo.SmartContract;
using Neo.VM;

namespace Neo.BlockchainToolkit.TraceDebug
{
    public interface ITraceDebugSink : IDisposable
    {
        void Trace(VMState vmState, long gasConsumed, IReadOnlyCollection<ExecutionContext> executionContexts);
        void Log(LogEventArgs args, string scriptName);
        void Notify(NotifyEventArgs args, string scriptName);
        void Results(VMState vmState, long gasConsumed, IReadOnlyCollection<Neo.VM.Types.StackItem> results);
        void Fault(Exception exception);
        void Script(Script script);
        void Storages(UInt160 scriptHash, IEnumerable<(StorageKey key, StorageItem item)> storages);
        void ProtocolSettings(uint network, byte addressVersion);
    }
}
