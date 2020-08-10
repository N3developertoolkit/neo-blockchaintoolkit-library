using System.Buffers;
using System.Collections.Generic;
using MessagePack;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public readonly struct ResultsRecord : ITraceDebugRecord
    {
        public const int RecordKey = 3;

        [Key(0)]
        public readonly VMState State;
        [Key(1)]
        public readonly long GasConsumed;
        [Key(2)]
        public readonly IReadOnlyList<StackItem> ResultStack;

        public ResultsRecord(VMState vmState, long gasConsumed, IReadOnlyList<StackItem> resultStack)
        {
            State = vmState;
            GasConsumed = gasConsumed;
            ResultStack = resultStack;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, VMState vmState, long gasConsumed, IReadOnlyCollection<StackItem> resultStack)
        {
            var mpWriter = new MessagePackWriter(writer);
            mpWriter.WriteArrayHeader(2);
            mpWriter.WriteInt32(RecordKey);
            mpWriter.WriteArrayHeader(3);
            options.Resolver.GetFormatterWithVerify<VMState>().Serialize(ref mpWriter, vmState, options);
            mpWriter.Write(gasConsumed);
            mpWriter.WriteArrayHeader(resultStack.Count);
            foreach (var item in resultStack)
            {
                options.Resolver.GetFormatterWithVerify<StackItem>().Serialize(ref mpWriter, item, options);
            }
            mpWriter.Flush();
        }

    }
}
