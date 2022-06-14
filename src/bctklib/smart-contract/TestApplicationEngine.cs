using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
        readonly Dictionary<UInt160, Dictionary<int, (int branchCount, int continueCount)>> branchMaps = new();
        readonly WitnessChecker witnessChecker;

        public IReadOnlyDictionary<UInt160, OneOf<ContractState, Script>> ExecutedScripts => executedScripts;

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

        public TestApplicationEngine(TriggerType trigger, IVerifiable? container, DataCache snapshot, Block? persistingBlock, ProtocolSettings settings, long gas, WitnessChecker? witnessChecker, IDiagnostic? diagnostic = null)
            : base(trigger, container ?? CreateTestTransaction(), snapshot, persistingBlock, settings, gas, diagnostic)
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

        public IReadOnlyDictionary<int, int> GetHitMap(UInt160 contractHash)
        {
            if (hitMaps.TryGetValue(contractHash, out var hitMap))
            {
                return hitMap;
            }

            return ImmutableDictionary<int, int>.Empty;
        }

        public IReadOnlyDictionary<int, (int branchCount, int continueCount)> GetBranchMap(UInt160 contractHash)
        {
            if (branchMaps.TryGetValue(contractHash, out var branchMap))
            {
                return branchMap;
            }

            return ImmutableDictionary<int, (int, int)>.Empty;
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

        static int CalculateBranchOffset(ExecutionContext context)
        {
            Debug.Assert(context.CurrentInstruction.IsBranchInstruction());

            switch (context.CurrentInstruction.OpCode)
            {
                case OpCode.JMPIF_L:
                case OpCode.JMPIFNOT_L:
                case OpCode.JMPEQ_L:
                case OpCode.JMPNE_L:
                case OpCode.JMPGT_L:
                case OpCode.JMPGE_L:
                case OpCode.JMPLT_L:
                case OpCode.JMPLE_L:
                    return context.InstructionPointer + context.CurrentInstruction.TokenI32;
                case OpCode.JMPIF:
                case OpCode.JMPIFNOT:
                case OpCode.JMPEQ:
                case OpCode.JMPNE:
                case OpCode.JMPGT:
                case OpCode.JMPGE:
                case OpCode.JMPLT:
                case OpCode.JMPLE:
                    return context.InstructionPointer + context.CurrentInstruction.TokenI8;
                default:
                    throw new InvalidOperationException($"CalculateBranchOffsets:GetOffset Unexpected OpCode {context.CurrentInstruction.OpCode}");
            }
        }

        record BranchInstructionInfo
        {
            public UInt160 ContractHash { get; init; } = UInt160.Zero;
            public int InstructionPointer { get; init; }
            public int BranchOffset { get; init; }
        }

        BranchInstructionInfo? branchInstructionInfo = null;

        protected override void PreExecuteInstruction()
        {
            base.PreExecuteInstruction();

            if (CurrentContext == null) return;

            var hash = CurrentContext.GetScriptHash()
                ?? throw new InvalidOperationException("CurrentContext.GetScriptHash returned null");

            if (CurrentContext.CurrentInstruction.IsBranchInstruction())
            {
                var branchOffset = CalculateBranchOffset(CurrentContext);
                branchInstructionInfo = new BranchInstructionInfo()
                {
                    ContractHash = hash,
                    InstructionPointer = CurrentContext.InstructionPointer,
                    BranchOffset = branchOffset,
                };
            }
            else
            {
                branchInstructionInfo = null;
            }

            if (!hitMaps.TryGetValue(hash, out var hitMap))
            {
                hitMap = new Dictionary<int, int>();
                hitMaps.Add(hash, hitMap);
            }

            var hitCount = hitMap.TryGetValue(CurrentContext.InstructionPointer, out var _hitCount) ? _hitCount : 0;
            hitMap[CurrentContext.InstructionPointer] = hitCount + 1;
        }

        protected override void PostExecuteInstruction()
        {
            base.PostExecuteInstruction();

            if (CurrentContext == null) return;

            if (branchInstructionInfo != null)
            {
                if (!branchMaps.TryGetValue(branchInstructionInfo.ContractHash, out var branchMap))
                {
                    branchMap = new Dictionary<int, (int, int)>();
                    branchMaps.Add(branchInstructionInfo.ContractHash, branchMap);
                }

                var branchHit = branchMap.TryGetValue(branchInstructionInfo.InstructionPointer, out var _branchCount)
                    ? _branchCount : (branchCount: 0, continueCount: 0);

                if (CurrentContext.InstructionPointer == branchInstructionInfo.BranchOffset)
                {
                    branchHit = (branchCount: branchHit.branchCount + 1, continueCount: branchHit.continueCount);
                }
                else if (CurrentContext.InstructionPointer == branchInstructionInfo.InstructionPointer)
                {
                    branchHit = (branchCount: branchHit.branchCount, continueCount: branchHit.continueCount + 1);
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected InstructionPointer {CurrentContext.InstructionPointer}");
                }

                branchMap[branchInstructionInfo.InstructionPointer] = branchHit;
            }
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
