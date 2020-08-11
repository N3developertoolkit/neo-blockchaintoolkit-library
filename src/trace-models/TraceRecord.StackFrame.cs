using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Neo.SmartContract;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    public partial class TraceRecord
    {
        [MessagePackObject]
        public class StackFrame
        {
            [Key(0)]
            public readonly UInt160 ScriptHash;
            [Key(1)]
            public readonly int InstructionPointer;
            [Key(2)]
            public readonly bool HasCatch;
            [Key(3)]
            public readonly IReadOnlyList<StackItem> EvaluationStack;
            [Key(4)]
            public readonly IReadOnlyList<StackItem> LocalVariables;
            [Key(5)]
            public readonly IReadOnlyList<StackItem> StaticFields;
            [Key(6)]
            public readonly IReadOnlyList<StackItem> Arguments;

            public StackFrame(
                UInt160 scriptHash,
                int instructionPointer,
                bool hasCatch,
                IReadOnlyList<StackItem> evaluationStack,
                IReadOnlyList<StackItem> localVariables,
                IReadOnlyList<StackItem> staticFields,
                IReadOnlyList<StackItem> arguments)
            {
                ScriptHash = scriptHash;
                InstructionPointer = instructionPointer;
                HasCatch = hasCatch;
                EvaluationStack = evaluationStack;
                LocalVariables = localVariables;
                StaticFields = staticFields;
                Arguments = arguments;
            }

            internal static void Write(ref MessagePackWriter writer, MessagePackSerializerOptions options, ExecutionContext context)
            {
                var stackItemCollectionResolver = options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<StackItem>>();

                writer.WriteArrayHeader(7);
                options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, context.GetScriptHash(), options);
                writer.Write(context.InstructionPointer);
                writer.Write(context.TryStack?.Any(c => c.HasCatch) == true);
                stackItemCollectionResolver.Serialize(ref writer, context.EvaluationStack, options);
                stackItemCollectionResolver.Serialize(ref writer, context.LocalVariables, options);
                stackItemCollectionResolver.Serialize(ref writer, context.StaticFields, options);
                stackItemCollectionResolver.Serialize(ref writer, context.Arguments, options);
            }
        }
    }
}
