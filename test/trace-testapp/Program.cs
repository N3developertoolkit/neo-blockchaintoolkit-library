using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Resolvers;
using Neo.BlockchainToolkit.TraceDebug;
using Nerdbank.Streams;

namespace trace_testapp
{
    class Program
    {
        const string PATH = @"C:\Users\harry\Source\neo\seattle\express\src\nxp3\0x85b99e4467e34df2308b7471c4335cee1c76b241d54c7dc5a61188e02eab4e1f.neo-trace";
        static readonly MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard
            .WithResolver(TraceDebugResolver.Instance);

        static void Foo(IBufferWriter<byte> bufferWriter, string msg)
        {
            var writer = new MessagePackWriter(bufferWriter);
            writer.WriteArrayHeader(2);
            writer.WriteInt32(2);
            writer.WriteArrayHeader(2);
            options.Resolver.GetFormatterWithVerify<Neo.UInt160>().Serialize(ref writer, Neo.UInt160.Zero, options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, msg, options);
            writer.Flush();
        }

        private static byte[] Foo()
        {
            var seq = new Sequence<byte>();
            Foo(seq, "test one");
            Foo(seq, "test two");
            var roseq = seq.AsReadOnlySequence;

            var stream = new MemoryStream();
            foreach (var seg in roseq)
            {
                stream.Write(seg.Span);
            }
            return stream.ToArray();

            // MessagePackSerializer.Serialize<ITraceRecord>(seq, new Log(Neo.UInt160.Zero, "test one"), options);
            // MessagePackSerializer.Serialize<ITraceRecord>(seq, new Log(Neo.UInt160.Zero, "test two"), options);

            // var roSeq = seq.AsReadOnlySequence;
            // while (roSeq.Length > 0)
            // {
            //     var rec = MessagePackSerializer.Deserialize<ITraceRecord>(roSeq, options);
            // }

        }
        private static async Task Main()
        {
            using var stream = File.OpenRead(PATH);

            while (stream.Position < stream.Length)
            {
                var rec = MessagePackSerializer.Deserialize<ITraceDebugRecord>(stream, options);
                Console.WriteLine(rec.GetType().Name);
                if (rec is TraceRecord tp)
                {
                    Console.WriteLine($"\t{tp.StackFrames.Count}");
                }
            }
        }
    }
}
