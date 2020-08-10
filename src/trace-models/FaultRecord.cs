using System.Buffers;
using MessagePack;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public readonly struct FaultRecord : ITraceDebugRecord
    {
        public const int RecordKey = 4;

        [Key(0)]
        public readonly string Exception;

        public FaultRecord(string exception)
        {
            Exception = exception;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, string exception)
        {
            var mpWriter = new MessagePackWriter(writer);
            mpWriter.WriteArrayHeader(2);
            mpWriter.WriteInt32(RecordKey);
            mpWriter.WriteArrayHeader(1);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref mpWriter, exception, options);
            mpWriter.Flush();
        }

    }
}
