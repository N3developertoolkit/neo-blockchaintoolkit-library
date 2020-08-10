using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public readonly struct NotifyRecord : ITraceDebugRecord
    {
        public const int RecordKey = 1;

        [Key(0)]
        public readonly UInt160 ScriptHash;
        [Key(1)]
        public readonly string EventName;
        [Key(2)]
        public readonly IReadOnlyList<StackItem> State;

        public NotifyRecord(UInt160 scriptHash, string eventName, IReadOnlyList<StackItem> state)
        {
            ScriptHash = scriptHash;
            EventName = eventName;
            State = state;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, UInt160 scriptHash, string eventName, IReadOnlyCollection<StackItem> state)
        {
            var mpWriter = new MessagePackWriter(writer);
            mpWriter.WriteArrayHeader(2);
            mpWriter.WriteInt32(RecordKey);
            mpWriter.WriteArrayHeader(3);
            options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref mpWriter, scriptHash, options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref mpWriter, eventName, options);
            mpWriter.WriteArrayHeader(state.Count);
            foreach (var item in state)
            {
                options.Resolver.GetFormatterWithVerify<StackItem>().Serialize(ref mpWriter, item, options);
            }
            mpWriter.Flush();
        }
    }
}
