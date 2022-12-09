using System;
using System.IO.Abstractions;
using Neo.SmartContract;
using Neo.VM;

using BinaryWriter = System.IO.BinaryWriter;
using FileMode = System.IO.FileMode;
using FileAccess = System.IO.FileAccess;
using FileShare = System.IO.FileShare;
using IOException = System.IO.IOException;
using Stream = System.IO.Stream;
using StreamWriter = System.IO.StreamWriter;
using TextWriter = System.IO.TextWriter;

namespace Neo.BlockchainToolkit.SmartContract
{
    public partial class TestApplicationEngine
    {
        class CoverageWriter : IDisposable
        {
            readonly string coveragePath;
            readonly Stream stream;
            readonly TextWriter writer;
            readonly IFileSystem fileSystem;
            bool disposed = false;

            public CoverageWriter(string coveragePath, IFileSystem? fileSystem = null)
            {
                this.fileSystem = fileSystem ?? new FileSystem();
                this.coveragePath = coveragePath;
                if (!this.fileSystem.Directory.Exists(coveragePath)) 
                {
                    this.fileSystem.Directory.CreateDirectory(coveragePath);
                }
                var filename = this.fileSystem.Path.Combine(coveragePath, $"{Guid.NewGuid()}.neo-coverage");

                stream = this.fileSystem.File.Create(filename);
                writer = new StreamWriter(stream);
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    writer.Flush();
                    stream.Flush();
                    writer.Dispose();
                    stream.Dispose();
                    disposed = true;
                }
            }

            public void WriteContext(ExecutionContext? context)
            {
                if (disposed) throw new ObjectDisposedException(nameof(CoverageWriter));

                if (context is null)
                {
                    writer.WriteLine($"{UInt160.Zero}");
                }
                else 
                {
                    var state = context.GetState<ExecutionContextState>();
                    var hash = context.Script.CalculateScriptHash(); 
                    writer.WriteLine($"{hash}");

                    if (state.Contract?.Nef is null)
                    {
                        var scriptPath = fileSystem.Path.Combine(coveragePath, $"{hash}.neo-script");
                        WriteScriptFile(scriptPath, stream => stream.Write(context.Script.AsSpan()));
                    }
                    else
                    {
                        var scriptPath = fileSystem.Path.Combine(coveragePath, $"{hash}.nef");
                        WriteScriptFile(scriptPath, stream => {
                            var writer = new BinaryWriter(stream);
                            state.Contract.Nef.Serialize(writer);
                            writer.Flush();
                        });
                    }
                }
            }

            void WriteScriptFile(string filename, Action<Stream> writeFileAction)
            {
                if (!fileSystem.File.Exists(filename))
                {
                    try
                    {
                        using var stream = fileSystem.File.Open(filename, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        writeFileAction(stream);
                        stream.Flush();
                    }
                    // ignore IOException thrown because file already exists
                    catch (IOException) { }
                }
            }

            // WriteAddress and WriteBranch do not need disposed check since writer will be disposed
            public void WriteAddress(int ip) => writer.WriteLine($"{ip}");

            public void WriteBranch(int ip, int offset, int result) => writer.WriteLine($"{ip} {offset} {result}");
        }
    }
}
