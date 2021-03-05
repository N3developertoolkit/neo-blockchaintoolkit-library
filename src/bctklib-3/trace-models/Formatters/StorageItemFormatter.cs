using System.Buffers;
using Neo.SmartContract;

namespace MessagePack.Formatters.Neo.BlockchainToolkit.TraceDebug
{
    public class StorageItemFormatter : IMessagePackFormatter<StorageItem>
    {
        public readonly static StorageItemFormatter Instance = new StorageItemFormatter();

        public StorageItem Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.ReadArrayHeader() != 1)
            {
                throw new MessagePackSerializationException();
            }

            var value = reader.ReadBytes()?.ToArray() ?? throw new MessagePackSerializationException();
            return new StorageItem(value);
        }

        public void Serialize(ref MessagePackWriter writer, StorageItem value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(1);
            writer.Write(value.Value);
        }
    }
}
