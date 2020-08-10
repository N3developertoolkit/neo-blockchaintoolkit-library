using System.Buffers;
using System.Collections.Generic;
using MessagePack;
using Neo.SmartContract;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public readonly struct StackFrameRecord
    {
        [Key(0)]
        public readonly UInt160 ScriptHash;
        [Key(1)]
        public readonly int InstructionPointer;
        [Key(2)]
        public readonly IReadOnlyCollection<StackItem> EvaluationStack;
        [Key(3)]
        public readonly IReadOnlyList<StackItem> LocalVariables;
        [Key(4)]
        public readonly IReadOnlyList<StackItem> StaticFields;
        [Key(5)]
        public readonly IReadOnlyList<StackItem> Arguments;

        public StackFrameRecord(
            UInt160 scriptHash,
            int instructionPointer,
            IReadOnlyCollection<StackItem> evaluationStack,
            IReadOnlyList<StackItem> localVariables,
            IReadOnlyList<StackItem> staticFields,
            IReadOnlyList<StackItem> arguments)
        {
            ScriptHash = scriptHash;
            InstructionPointer = instructionPointer;
            EvaluationStack = evaluationStack;
            LocalVariables = localVariables;
            StaticFields = staticFields;
            Arguments = arguments;
        }

        internal static void Write(ref MessagePackWriter writer, MessagePackSerializerOptions options, ExecutionContext context)
        {
            var resolver = options.Resolver;
            writer.WriteArrayHeader(6);
            resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, context.GetScriptHash(), options);
            writer.Write(context.InstructionPointer);
            var stackItemCollectionResolver = resolver.GetFormatterWithVerify<IReadOnlyCollection<StackItem>>();
            stackItemCollectionResolver.Serialize(ref writer, context.EvaluationStack, options);
            stackItemCollectionResolver.Serialize(ref writer, context.LocalVariables, options);
            stackItemCollectionResolver.Serialize(ref writer, context.StaticFields, options);
            stackItemCollectionResolver.Serialize(ref writer, context.Arguments, options);
        }
    }
}
