using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json.Linq;
using Xunit;


namespace test.bctklib3
{
    public class DebugInfoTest
    {
        const string TEST_HASH = "0xf69e5188632deb3a9273519efc86cb68da8d42b8";

        [Fact]
        public async Task can_load_debug_json()
        {
            var debugInfoJson = GetResource("Registrar.debug.json");
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\fakeContract.nef", new MockFileData("") },
                { @"c:\fakeContract.debug.json", new MockFileData(debugInfoJson) },
            });
            var debugInfo = await DebugInfo.LoadAsync(@"c:\fakeContract.nef", null, fileSystem);
            Assert.True(debugInfo.IsT0);
        }

        [Fact]
        public async Task can_load_nefdbgnfo()
        {
            var debugInfoJson = GetResource("Registrar.debug.json");
            var compressedDebugInfo = CreateCompressedDebugInfo("fakeContract", debugInfoJson);
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\fakeContract.nef", new MockFileData("") },
                { @"c:\fakeContract.nefdbgnfo", new MockFileData(compressedDebugInfo) },
            });
            var debugInfo = await DebugInfo.LoadAsync(@"c:\fakeContract.nef", null, fileSystem);
            Assert.True(debugInfo.IsT0);
        }

        [Fact]
        public async Task not_found_debug_info()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\fakeContract.nef", new MockFileData("") },
            });
            var debugInfo = await DebugInfo.LoadAsync(@"c:\fakeContract.nef", null, fileSystem);
            Assert.True(debugInfo.IsT1);
        }

        [Fact]
        public void can_load_minimal_debug_info()
        {
            var json = new JObject(new JProperty("hash", TEST_HASH));

            var debugInfo = DebugInfo.Load(json, t => t.Value<string>()!);
            Assert.Equal(UInt160.Parse(TEST_HASH), debugInfo.ScriptHash);
            Assert.Empty(debugInfo.Documents);
            Assert.Empty(debugInfo.Events);
            Assert.Empty(debugInfo.Methods);
            Assert.Empty(debugInfo.StaticVariables);
        }

        [Fact]
        public void cant_load_debug_info_without_hash()
        {
            var debugInfoJson = GetResource("Registrar.debug.json");
            var json = JObject.Parse(debugInfoJson);
            json.Remove("hash");

            var ex = Assert.Throws<FormatException>(() => DebugInfo.Load(json, t => t.Value<string>()!));
            Assert.Equal("Missing hash value", ex.Message);
        }

        [Fact]
        public void resolve_source_current_directory()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\src\Apoc.cs", new MockFileData("") },
                { @"c:\src\Apoc.Crowdsale.cs", new MockFileData("") },
            }, @"c:\src");
            var resolver = new DebugInfo.DocumentResolver(ImmutableDictionary<string, string>.Empty, fileSystem);

            var actual = resolver.ResolveDocument(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs");

            Assert.Equal(@"c:\src\Apoc.cs", actual);
        }

        [Fact]
        public void resolve_source_files_exist()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs", new MockFileData("") },
            }, @"c:\src");
            var resolver = new DebugInfo.DocumentResolver(ImmutableDictionary<string, string>.Empty, fileSystem);

            var actual = resolver.ResolveDocument(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs");

            Assert.Equal(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs", actual);
        }

        [Fact]
        public void resolve_source_files_dont_exist()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
            }, @"c:\src");
            var resolver = new DebugInfo.DocumentResolver(ImmutableDictionary<string, string>.Empty, fileSystem);

            var actual = resolver.ResolveDocument(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs");

            Assert.Equal(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs", actual);
        }

        [Fact]
        public void resolve_source_via_map()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\src\Apoc.cs", new MockFileData("") },
            });
            var sourceMap = new Dictionary<string, string>
            {
                { @"c:\Users\harry\Source\neo\seattle\samples\token-sample\src", @"c:\src"}
            };
            var resolver = new DebugInfo.DocumentResolver(sourceMap, fileSystem);

            var actual = resolver.ResolveDocument(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs");

            Assert.Equal(@"c:\src\Apoc.cs", actual);
        }

        [Fact]
        public void can_parse_static_variables()
        {
            var json = new JObject(
                new JProperty("hash", TEST_HASH),
                new JProperty("static-variables", 
                    new JArray(
                        "testStatic1,String", 
                        "testStatic2,Hash160")));

            var debugInfo = DebugInfo.Load(json, t => t.Value<string>()!);

            Assert.Collection(debugInfo.StaticVariables,
                s => {
                    Assert.Equal("testStatic1", s.Name);
                    Assert.Equal("String", s.Type);
                    Assert.Null(s.SlotIndex);
                },
                s => {
                    Assert.Equal("testStatic2", s.Name);
                    Assert.Equal("Hash160", s.Type);
                    Assert.Null(s.SlotIndex);
                });
        }

        [Fact]
        public void can_parse_static_variables_explicit_slot_indexes()
        {
            var json = new JObject(
                new JProperty("hash", TEST_HASH),
                new JProperty("static-variables", 
                    new JArray(
                        "testStatic1,String,1", 
                        "testStatic2,Hash160,3")));

            var debugInfo = DebugInfo.Load(json, t => t.Value<string>()!);

            Assert.Collection(debugInfo.StaticVariables,
                s => {
                    Assert.Equal("testStatic1", s.Name);
                    Assert.Equal("String", s.Type);
                    Assert.True(s.SlotIndex.HasValue);
                    Assert.Equal<uint>(1, s.SlotIndex!.Value);
                },
                s => {
                    Assert.Equal("testStatic2", s.Name);
                    Assert.Equal("Hash160", s.Type);
                    Assert.True(s.SlotIndex.HasValue);
                    Assert.Equal<uint>(3, s.SlotIndex!.Value);
                });
        }

        [Fact]
        public void can_load_debug_info_with_invalid_sequence_points()
        {
            var debugInfoJson = GetResource("invalidSequencePoints.json");
            var json = JObject.Parse(debugInfoJson);
            var debug = DebugInfo.Load(json, t => t.Value<string>()!);
        }

        static byte[] CreateCompressedDebugInfo(string contractName, string debugInfo)
        {
            var jsonDebugInfo = Neo.IO.Json.JObject.Parse(debugInfo);
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                using var stream = archive.CreateEntry($"{contractName}.debug.json").Open();
                stream.Write(jsonDebugInfo.ToByteArray(false));
            }
            return memoryStream.ToArray();
        }

        static string GetResource(string name)
        {
            var assembly = typeof(DebugInfoTest).Assembly;
            using var resource = assembly.GetManifestResourceStream(name)
                ?? assembly.GetManifestResourceStream($"test.bctklib-3._testFiles.{name}")
                ?? throw new FileNotFoundException();
            using var streamReader = new System.IO.StreamReader(resource);
            return streamReader.ReadToEnd();
        }
    }
}