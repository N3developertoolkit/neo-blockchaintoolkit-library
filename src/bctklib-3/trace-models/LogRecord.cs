using System.Buffers;
using MessagePack;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public class LogRecord : ITraceDebugRecord
    {
        public const int RecordKey = 2;

        [Key(0)]
        public readonly UInt160 ScriptHash;
        [Key(1)]
        public readonly string Message;

        public LogRecord(UInt160 scriptHash, string message)
        {
            ScriptHash = scriptHash;
            Message = message;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, UInt160 scriptHash, string message)
        {
            var mpWriter = new MessagePackWriter(writer);
            mpWriter.WriteArrayHeader(2);
            mpWriter.WriteInt32(RecordKey);
            mpWriter.WriteArrayHeader(2);
            options.Resolver.GetFormatterWithVerify<Neo.UInt160>().Serialize(ref mpWriter, scriptHash, options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref mpWriter, message, options);
            mpWriter.Flush();
        }
    }
}
