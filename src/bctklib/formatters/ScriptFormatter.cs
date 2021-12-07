using Neo.VM;

namespace MessagePack.Formatters.Neo.BlockchainToolkit
{
    public class ScriptFormatter : IMessagePackFormatter<Script>
    {
        public static readonly ScriptFormatter Instance = new ScriptFormatter();

        public Script Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return options.Resolver.GetFormatter<byte[]>().Deserialize(ref reader, options);
        }

        public void Serialize(ref MessagePackWriter writer, Script value, MessagePackSerializerOptions options)
        {
            writer.Write((byte[])value);
        }
    }
}
