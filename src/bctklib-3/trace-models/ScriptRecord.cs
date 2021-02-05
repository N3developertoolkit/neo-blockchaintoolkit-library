using System.Buffers;
using MessagePack;
using Neo.VM;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public class ScriptRecord : ITraceDebugRecord
    {
        public const int RecordKey = 5;

        [Key(0)]
        public readonly UInt160 ScriptHash;

        [Key(1)]
        public readonly Script Script;

        public ScriptRecord(UInt160 scriptHash, Script script)
        {
            ScriptHash = scriptHash;
            Script = script;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, byte[] script)
        {
            var scriptHash = Neo.SmartContract.Helper.ToScriptHash(script);

            var mpWriter = new MessagePackWriter(writer);
            mpWriter.WriteArrayHeader(2);
            mpWriter.WriteInt32(RecordKey);
            mpWriter.WriteArrayHeader(2);
            options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref mpWriter, scriptHash, options);
            options.Resolver.GetFormatterWithVerify<byte[]>().Serialize(ref mpWriter, script, options);
            mpWriter.Flush();
        }
    }
}
