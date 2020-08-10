using System.Buffers;
using System.Collections.Generic;
using MessagePack;
using Neo.VM;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public readonly partial struct TraceRecord : ITraceDebugRecord
    {
        public const int RecordKey = 0;

        [Key(0)]
        public readonly VMState State;
        [Key(1)]
        public readonly IReadOnlyCollection<StackFrame> StackFrames;

        public TraceRecord(VMState state, IReadOnlyCollection<StackFrame> stackFrames)
        {
            State = state;
            StackFrames = stackFrames;
        }

        public static void Write(IBufferWriter<byte> writer,
                                 MessagePackSerializerOptions options,
                                 VMState vmState,
                                 IReadOnlyCollection<ExecutionContext> contexts)
        {
            var resolver = options.Resolver;
            var mpWriter = new MessagePackWriter(writer);
            mpWriter.WriteArrayHeader(2);
            mpWriter.WriteInt32(RecordKey);
            mpWriter.WriteArrayHeader(2);
            resolver.GetFormatterWithVerify<VMState>().Serialize(ref mpWriter, vmState, options);
            mpWriter.WriteArrayHeader(contexts.Count);
            foreach (var context in contexts)
            {
                StackFrame.Write(ref mpWriter, options, context);
            }
            mpWriter.Flush();
        }
    }
}
