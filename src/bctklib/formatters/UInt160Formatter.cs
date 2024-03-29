using System;
using System.Buffers;
using Neo;
using Neo.IO;

namespace MessagePack.Formatters.Neo.BlockchainToolkit
{
    public class UInt160Formatter : IMessagePackFormatter<UInt160>
    {
        public static readonly UInt160Formatter Instance = new UInt160Formatter();

        public UInt160 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            // post RC3 serialization format of UInt160
            if (reader.NextMessagePackType == MessagePackType.Binary)
            {
                var value = options.Resolver.GetFormatter<byte[]>().Deserialize(ref reader, options);
                return new UInt160(value);
            }

            // pre RC3 serialization format of UInt160
            if (reader.NextMessagePackType == MessagePackType.Integer)
            {
                var seq = reader.ReadRaw(UInt160.Length);
                return new UInt160(seq.IsSingleSegment ? seq.FirstSpan : seq.ToArray());
            }

            throw new MessagePackSerializationException($"Unexpected UInt160 MessagePack type {reader.NextMessagePackType}");
        }

        public void Serialize(ref MessagePackWriter writer, UInt160 value, MessagePackSerializerOptions options)
        {
            writer.Write(value.ToArray());
        }
    }
}
