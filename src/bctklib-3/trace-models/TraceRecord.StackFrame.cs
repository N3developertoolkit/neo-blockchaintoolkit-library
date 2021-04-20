using System;
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
        // Note, Neo 3 calculates the script hash used to identify a contract from the script binary at initial deployment + the address of the contract deployer
        // This enables a stable contract identifier, even as the contract is later updated.
        // However, debugging requires the SHA 256 hash of the script binary to tie a specific contract version to its associated debug info.

        // In TraceRecord:
        //   * ScriptIdentifier property is the UInt160 value created on script deployment that identifies a contract (even after updates)
        //   * ScriptHash is the SHA 256 hash of the script binary, needed to map scripts to debug info

        [MessagePackObject]
        public class StackFrame
        {
            [Key(0)]
            public readonly UInt160 ScriptIdentifier;
            [Key(1)]
            public readonly UInt160 ScriptHash;
            [Key(2)]
            public readonly int InstructionPointer;
            [Key(3)]
            public readonly bool HasCatch;
            [Key(4)]
            public readonly IReadOnlyList<StackItem> EvaluationStack;
            [Key(5)]
            public readonly IReadOnlyList<StackItem> LocalVariables;
            [Key(6)]
            public readonly IReadOnlyList<StackItem> StaticFields;
            [Key(7)]
            public readonly IReadOnlyList<StackItem> Arguments;

            public StackFrame(
                UInt160 scriptIdentifier,
                UInt160 scriptHash,
                int instructionPointer,
                bool hasCatch,
                IReadOnlyList<StackItem> evaluationStack,
                IReadOnlyList<StackItem> localVariables,
                IReadOnlyList<StackItem> staticFields,
                IReadOnlyList<StackItem> arguments)
            {
                ScriptIdentifier = scriptIdentifier;
                ScriptHash = scriptHash;
                InstructionPointer = instructionPointer;
                HasCatch = hasCatch;
                EvaluationStack = evaluationStack;
                LocalVariables = localVariables;
                StaticFields = staticFields;
                Arguments = arguments;
            }

            internal static void Write(ref MessagePackWriter writer, MessagePackSerializerOptions options, IDictionary<UInt160, UInt160> scriptIdMap, ExecutionContext context)
            {
                var scriptId = context.GetScriptHash();
                if (!scriptIdMap.TryGetValue(scriptId, out var scriptHash))
                {
                    scriptHash = Neo.SmartContract.Helper.ToScriptHash(context.Script);
                    scriptIdMap[scriptId] = scriptHash;
                }

                var stackItemCollectionResolver = options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<StackItem>>();

                writer.WriteArrayHeader(8);
                options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, scriptId, options);
                options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, scriptHash, options);
                writer.Write(context.InstructionPointer);
                writer.Write(context.TryStack?.Any(c => c.HasCatch) == true);
                stackItemCollectionResolver.Serialize(ref writer, context.EvaluationStack, options);
                stackItemCollectionResolver.Serialize(ref writer, Coalese(context.LocalVariables), options);
                stackItemCollectionResolver.Serialize(ref writer, Coalese(context.StaticFields), options);
                stackItemCollectionResolver.Serialize(ref writer, Coalese(context.Arguments), options);

                static IReadOnlyList<StackItem> Coalese(Neo.VM.Slot? slot) => (slot == null) ? Array.Empty<StackItem>() : slot;
            }
        }
    }
}
