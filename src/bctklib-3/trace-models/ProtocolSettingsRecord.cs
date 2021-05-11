using System.Buffers;
using MessagePack;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public class ProtocolSettingsRecord : ITraceDebugRecord
    {
        public const int RecordKey = 7;

        [Key(0)]
        public readonly uint Network;

        [Key(1)]
        public readonly byte AddressVersion;

        public ProtocolSettingsRecord(uint network, byte addressVersion)
        {
            Network = network;
            AddressVersion = addressVersion;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, uint network, byte addressVersion)
        {
            var mpWriter = new MessagePackWriter(writer);
            mpWriter.WriteArrayHeader(2);
            mpWriter.WriteInt32(RecordKey);
            mpWriter.WriteArrayHeader(2);
            options.Resolver.GetFormatterWithVerify<uint>().Serialize(ref mpWriter, network, options);
            options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref mpWriter, addressVersion, options);
            mpWriter.Flush();
        }
    }
}
