using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using OneOf;

namespace Neo.BlockchainToolkit.SmartContract
{
    using WitnessChecker = Func<byte[], bool>;

    public partial class TestApplicationEngine : ApplicationEngine
    {
        readonly static IReadOnlyDictionary<uint, InteropDescriptor> overriddenServices;

        static TestApplicationEngine()
        {
            var builder = ImmutableDictionary.CreateBuilder<uint, InteropDescriptor>();
            builder.Add(OverrideDescriptor(ApplicationEngine.System_Runtime_CheckWitness, nameof(CheckWitnessOverride)));
            overriddenServices = builder.ToImmutable();

            static KeyValuePair<uint, InteropDescriptor> OverrideDescriptor(InteropDescriptor descriptor, string overrideMethodName)
            {
                var overrideMethodInfo = typeof(TestApplicationEngine).GetMethod(overrideMethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException($"{nameof(OverrideDescriptor)} failed to locate {overrideMethodName} method");
                return KeyValuePair.Create(descriptor.Hash, descriptor with { Handler = overrideMethodInfo });
            }
        }

        public static Block CreateDummyBlock(DataCache snapshot, ProtocolSettings settings)
        {
            var hash = NativeContract.Ledger.CurrentHash(snapshot);
            var currentBlock = NativeContract.Ledger.GetBlock(snapshot, hash);

            return new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = hash,
                    MerkleRoot = new UInt256(),
                    Timestamp = currentBlock.Timestamp + settings.MillisecondsPerBlock,
                    Index = currentBlock.Index + 1,
                    NextConsensus = currentBlock.NextConsensus,
                    Witness = new Witness
                    {
                        InvocationScript = Array.Empty<byte>(),
                        VerificationScript = Array.Empty<byte>()
                    },
                },
                Transactions = Array.Empty<Transaction>()
            };
        }

        public static Transaction CreateTestTransaction(UInt160 signerAccount, WitnessScope witnessScope = WitnessScope.CalledByEntry)
            => CreateTestTransaction(new Signer
            {
                Account = signerAccount,
                Scopes = witnessScope,
                AllowedContracts = Array.Empty<UInt160>(),
                AllowedGroups = Array.Empty<Neo.Cryptography.ECC.ECPoint>()
            });

        public static Transaction CreateTestTransaction(Signer? signer = null) => new Transaction
        {
            Nonce = (uint)new Random().Next(),
            Script = Array.Empty<byte>(),
            Signers = signer == null ? Array.Empty<Signer>() : new[] { signer },
            Attributes = Array.Empty<TransactionAttribute>(),
            Witnesses = Array.Empty<Witness>(),
        };

        readonly Dictionary<UInt160, OneOf<ContractState, Script>> executedScripts = new();
        readonly Dictionary<UInt160, Dictionary<int, int>> hitMaps = new();
        readonly WitnessChecker witnessChecker;


        public new event EventHandler<LogEventArgs>? Log;
        public new event EventHandler<NotifyEventArgs>? Notify;

        public TestApplicationEngine(DataCache snapshot, ProtocolSettings settings)
            : this(TriggerType.Application, null, snapshot, null, settings, ApplicationEngine.TestModeGas, null)
        {
        }

        public TestApplicationEngine(DataCache snapshot, ProtocolSettings settings, UInt160 signer, WitnessScope witnessScope = WitnessScope.CalledByEntry)
            : this(TriggerType.Application, CreateTestTransaction(signer, witnessScope), snapshot, null, settings, ApplicationEngine.TestModeGas, null)
        {
        }

        public TestApplicationEngine(DataCache snapshot, ProtocolSettings settings, Transaction transaction)
            : this(TriggerType.Application, transaction, snapshot, null, settings, ApplicationEngine.TestModeGas, null)
        {
        }

        public TestApplicationEngine(TriggerType trigger, IVerifiable? container, DataCache snapshot, Block? persistingBlock, ProtocolSettings settings, long gas, WitnessChecker? witnessChecker)
            : base(trigger, container ?? CreateTestTransaction(), snapshot, persistingBlock, settings, gas)
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

        protected override void LoadContext(ExecutionContext context)
        {
            base.LoadContext(context);

            var ecs = context.GetState<ExecutionContextState>();
            if (ecs.ScriptHash == null) throw new InvalidOperationException("ExecutionContextState.ScriptHash is null");
            if (!executedScripts.ContainsKey(ecs.ScriptHash))
            {
                if (ecs.Contract == null)
                {
                    executedScripts.Add(ecs.ScriptHash, context.Script);
                }
                else
                {
                    executedScripts.Add(ecs.ScriptHash, ecs.Contract);
                }
            }
        }

        protected override void PreExecuteInstruction()
        {
            if (CurrentContext == null) return;

            var hash = CurrentContext.GetScriptHash() 
                ?? throw new InvalidOperationException("CurrentContext.GetScriptHash returned null");

            if (!hitMaps.TryGetValue(hash, out var hitMap))
            {
                hitMap = new Dictionary<int, int>();
                hitMaps.Add(hash, hitMap);
            }

            var hitCount = hitMap.TryGetValue(CurrentContext.InstructionPointer, out var _hitCount) ? _hitCount : 0;
            hitMap[CurrentContext.InstructionPointer] = hitCount + 1;
        }

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

        bool CheckWitnessOverride(byte[] hashOrPubkey) => witnessChecker(hashOrPubkey);

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
