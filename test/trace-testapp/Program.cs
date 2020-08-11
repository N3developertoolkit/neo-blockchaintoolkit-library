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
    internal static class Program
    {
        private static void Main()
        {
            const string PATH = "C:/Users/harry/Source/neo/seattle/express/src/nxp3/0xbb00986f67974c2277ffbe79b7c60981dc7348a0bd724e56644e562538af5ae6.neo-trace";

            var options = MessagePackSerializerOptions.Standard
                .WithResolver(TraceDebugResolver.Instance);

            using var stream = File.OpenRead(PATH);

            while (stream.Position < stream.Length)
            {
                var rec = MessagePackSerializer.Deserialize<ITraceDebugRecord>(stream, options);
                Console.WriteLine(rec.GetType().Name);
                if (rec is TraceRecord tp)
                {
                    Console.WriteLine($"\t{tp.StackFrames.Count}");
                    // if (tp.StackFrames.Count > 0)
                    // {
                    //     Console.WriteLine($"\t{tp.StackFrames[0].InstructionPointer}");
                    // }
                }
            }
        }
    }
}
