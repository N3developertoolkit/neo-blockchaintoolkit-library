using System;
using DotNext.Buffers;

namespace Neo.BlockchainToolkit.Persistence
{
    static class SpanReaderExtensions
    {
        public static ulong ReadVarInt(this ref SpanReader<byte> reader, ulong max = ulong.MaxValue)
        {
            byte b = reader.Read();
            ulong value = b switch
            {
                0xfd => reader.ReadUInt16(true),
                0xfe => reader.ReadUInt32(true),
                0xff => reader.ReadUInt64(true),
                _ => b
            };
            if (value > max) throw new FormatException();
            return value;
        }

        public static bool ReadBoolean(this ref SpanReader<byte> reader)
        {
            return reader.Read() switch
            {
                0 => false,
                1 => true,
                _ => throw new FormatException()
            };
        }

        public static byte[] ReadVarBytes(this ref SpanReader<byte> reader, ulong max = 0x1000000)
        {
            var length = (int)reader.ReadVarInt(max);
            var buffer = new byte[length];
            reader.Read(buffer.AsSpan());
            return buffer;
        }
    }
}
