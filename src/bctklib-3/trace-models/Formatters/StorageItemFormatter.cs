using System.Buffers;
using Neo.Ledger;

namespace MessagePack.Formatters.Neo.BlockchainToolkit.TraceDebug
{
    public class StorageItemFormatter : IMessagePackFormatter<StorageItem>
    {
        public readonly static StorageItemFormatter Instance = new StorageItemFormatter();

        public StorageItem Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.ReadArrayHeader() != 2)
            {
                throw new MessagePackSerializationException();
            }

            var value = reader.ReadBytes()?.ToArray() ?? throw new MessagePackSerializationException();
            var isConstant = reader.ReadBoolean();
            return new StorageItem(value, isConstant);
        }

        public void Serialize(ref MessagePackWriter writer, StorageItem value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            writer.Write(value.Value);
            writer.Write(value.IsConstant);
        }
    }
}
