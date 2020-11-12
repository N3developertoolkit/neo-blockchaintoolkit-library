using System.Buffers;
using Neo.Ledger;
using Neo.VM;

namespace MessagePack.Formatters.Neo.BlockchainToolkit.TraceDebug
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
            options.Resolver.GetFormatter<byte[]>().Serialize(ref writer, value, options);
        }
    }
}
