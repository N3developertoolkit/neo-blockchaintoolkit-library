using System.Buffers;
using System.Collections.Generic;
using MessagePack;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public readonly struct ScriptRecord : ITraceDebugRecord
    {
        public const int RecordKey = 5;

        [Key(0)]
        public readonly UInt160 ScriptHash;

        [Key(1)]
        public readonly Script Script;

        public ScriptRecord(UInt160 scriptHash, byte[] script)
        {
            ScriptHash = scriptHash;
            Script = script;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, byte[] script)
        {
            var scriptHash = SmartContract.Helper.ToScriptHash(script);

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
