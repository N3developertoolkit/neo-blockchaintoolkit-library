using System;
using System.Collections.Generic;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;

namespace Neo.BlockchainToolkit.Plugins
{
    public sealed class ToolkitLogPlugin : Plugin
    {
        NeoSystem? neoSystem;
        readonly Action<string> writeLine;
        readonly Action<string> writeError;

        public ToolkitLogPlugin(Action<string> writeLine, Action<string> writeError)
        {
            this.writeLine = writeLine;
            this.writeError = writeError;

            Blockchain.Committing += OnCommitting;
            ApplicationEngine.Log += OnAppEngineLog!;
            Neo.Utility.Logging += OnNeoUtilityLog;
        }

        public override void Dispose()
        {
            Neo.Utility.Logging -= OnNeoUtilityLog;
            ApplicationEngine.Log -= OnAppEngineLog!;
            Blockchain.Committing -= OnCommitting;
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (this.neoSystem is not null) throw new Exception($"{nameof(OnSystemLoaded)} already called");
            neoSystem = system;
            base.OnSystemLoaded(system);
        }

        string GetContractName(UInt160 scriptHash)
        {
            if (neoSystem is not null)
            {
                var contract = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, scriptHash);
                if (contract is not null)
                {
                    return contract.Manifest.Name;
                }
            }

            return scriptHash.ToString();
        }

        void OnAppEngineLog(object sender, LogEventArgs args)
        {
            var container = args.ScriptContainer is null
                ? string.Empty
                : $" [{args.ScriptContainer.GetType().Name}]";
            writeLine($"\x1b[35m{GetContractName(args.ScriptHash)}\x1b[0m Log: \x1b[96m\"{args.Message}\"\x1b[0m{container}");
        }

        void OnNeoUtilityLog(string source, LogLevel level, object message)
        {
            writeLine($"{DateTimeOffset.Now:HH:mm:ss.ff} {source} {level} {message}");
        }

        void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            foreach (var appExec in applicationExecutedList)
            {
                OnApplicationExecuted(appExec);
            }
        }

        void OnApplicationExecuted(Neo.Ledger.Blockchain.ApplicationExecuted applicationExecuted)
        {
            if (applicationExecuted.VMState == Neo.VM.VMState.FAULT)
            {
                var logMessage = $"Tx FAULT: hash={applicationExecuted.Transaction.Hash}";
                if (!string.IsNullOrEmpty(applicationExecuted.Exception.Message))
                {
                    logMessage += $" exception=\"{applicationExecuted.Exception.Message}\"";
                }
                writeError($"\x1b[31m{logMessage}\x1b[0m");
            }
        }
    }
}
