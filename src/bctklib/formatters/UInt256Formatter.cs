using System.Buffers;
using Neo;
using Neo.IO;

namespace MessagePack.Formatters.Neo.BlockchainToolkit
{
    public class UInt256Formatter : IMessagePackFormatter<UInt256>
    {
        public static readonly UInt256Formatter Instance = new UInt256Formatter();

        public UInt256 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var value = options.Resolver.GetFormatter<byte[]>().Deserialize(ref reader, options);
            return new UInt256(value);
        }

        public void Serialize(ref MessagePackWriter writer, UInt256 value, MessagePackSerializerOptions options)
        {
            writer.Write(value.ToArray());
        }
    }
}
