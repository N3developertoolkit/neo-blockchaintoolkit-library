using System;
using System.Buffers;
using Neo;
using Neo.IO;

namespace MessagePack.Formatters.Neo.BlockchainToolkit.TraceDebug
{
    public class UInt160Formatter : IMessagePackFormatter<UInt160>
    {
        public static readonly UInt160Formatter Instance = new UInt160Formatter();

        public UInt160 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var seq = reader.ReadRaw(UInt160.Length);
            return new UInt160(seq.IsSingleSegment ? seq.FirstSpan : seq.ToArray());
        }

        public void Serialize(ref MessagePackWriter writer, UInt160 value, MessagePackSerializerOptions options)
        {
            writer.WriteRaw(value.ToArray().AsSpan(0, UInt160.Length));
        }
    }
}
