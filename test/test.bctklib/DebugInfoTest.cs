using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

#pragma warning disable VSTHRD200
namespace test.bctklib
{
    public class DebugInfoTest
    {
        const string TEST_HASH = "0xf69e5188632deb3a9273519efc86cb68da8d42b8";

        [Fact]
        public void can_load_v2_debug_info()
        {
            var debugInfoText = Utility.GetResource("v2-debug.json");
            var debugInfoJson = JObject.Parse(debugInfoText);
            var unboundStructs = DebugInfo.ParseUnboundStructs(debugInfoJson["structs"] as JArray ?? throw new Exception()).ToArray();
            var boundStructs = DebugInfo.BindStructs(unboundStructs);

            var debugInfo = DebugInfo.Load(debugInfoJson, token => token.Value<string>());
        }

        [Fact]
        public async Task can_load_debug_json_nccs_rc3()
        {
            var debugInfoJson = Utility.GetResource("nccs_rc3.json");
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            string nefPath = fileSystem.Path.Combine(rootPath, "fakeContract.nef");
            fileSystem.AddFile(nefPath, MockFileData.NullObject);
            fileSystem.AddFile(fileSystem.Path.Combine(rootPath, "fakeContract.debug.json"), new MockFileData(debugInfoJson));
            var debugInfo = await DebugInfo.LoadAsync(nefPath, null, fileSystem);
            Assert.True(debugInfo.IsT0);
        }

        [Fact]
        public async Task can_load_debug_json()
        {
            var debugInfoJson = Utility.GetResource("Registrar.debug.json");
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            string nefPath = fileSystem.Path.Combine(rootPath, "fakeContract.nef");
            fileSystem.AddFile(nefPath, MockFileData.NullObject);
            fileSystem.AddFile(fileSystem.Path.Combine(rootPath, "fakeContract.debug.json"), new MockFileData(debugInfoJson));
            var debugInfo = await DebugInfo.LoadAsync(nefPath, null, fileSystem);
            Assert.True(debugInfo.IsT0);
        }

        [Fact]
        public async Task can_load_nefdbgnfo()
        {
            var debugInfoJson = Utility.GetResource("Registrar.debug.json");
            var compressedDebugInfo = CreateCompressedDebugInfo("fakeContract", debugInfoJson);
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            string nefPath = fileSystem.Path.Combine(rootPath, "fakeContract.nef");
            fileSystem.AddFile(nefPath, MockFileData.NullObject);
            fileSystem.AddFile(fileSystem.Path.Combine(rootPath, "fakeContract.nefdbgnfo"), new MockFileData(compressedDebugInfo));
            var debugInfo = await DebugInfo.LoadAsync(nefPath, null, fileSystem);
            Assert.True(debugInfo.IsT0);
        }

        [Fact]
        public async Task not_found_debug_info()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            string nefPath = fileSystem.Path.Combine(rootPath, "fakeContract.nef");
            var debugInfo = await DebugInfo.LoadAsync(nefPath, null, fileSystem);
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
            var debugInfoJson = Utility.GetResource("Registrar.debug.json");
            var json = JObject.Parse(debugInfoJson);
            json.Remove("hash");

            var ex = Assert.Throws<JsonException>(() => DebugInfo.Load(json, t => t.Value<string>()!));
            Assert.Equal("Missing hash value", ex.Message);
        }

        [Fact]
        public void resolve_source_current_directory()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var srcPath = fileSystem.Path.Combine(rootPath, "src");
            fileSystem.Directory.SetCurrentDirectory(srcPath);
            var apocPath = fileSystem.Path.Combine(srcPath, "Apoc.cs");
            fileSystem.AddFile(apocPath, MockFileData.NullObject);
            fileSystem.AddFile(fileSystem.Path.Combine(srcPath, "Apoc.Crowdsale.cs"), MockFileData.NullObject);

            var resolver = new DebugInfo.DocumentResolver(ImmutableDictionary<string, string>.Empty, fileSystem);
            var actual = resolver.ResolveDocument(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs");

            Assert.Equal(apocPath, actual);
        }

        [Fact]
        public void resolve_source_files_exist()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var srcPath = fileSystem.Path.Combine(rootPath, "neo", "token-sample", "src");
            fileSystem.Directory.SetCurrentDirectory(srcPath);
            var apocPath = fileSystem.Path.Combine(srcPath, "Apoc.cs");
            fileSystem.AddFile(apocPath, MockFileData.NullObject);

            var resolver = new DebugInfo.DocumentResolver(ImmutableDictionary<string, string>.Empty, fileSystem);
            var actual = resolver.ResolveDocument(apocPath);

            Assert.Equal(apocPath, actual);
        }

        [Fact]
        public void resolve_source_files_dont_exist()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var srcPath = fileSystem.Path.Combine(rootPath, "neo", "token-sample", "src");
            fileSystem.Directory.SetCurrentDirectory(srcPath);
            var apocPath = fileSystem.Path.Combine(srcPath, "Apoc.cs");

            var resolver = new DebugInfo.DocumentResolver(ImmutableDictionary<string, string>.Empty, fileSystem);
            var actual = resolver.ResolveDocument(apocPath);

            Assert.Equal(apocPath, actual);
        }

        [Fact]
        public void resolve_source_via_map()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var srcPath = fileSystem.Path.Combine(rootPath, "neo", "token-sample", "src");
            var apocPath = fileSystem.Path.Combine(srcPath, "Apoc.cs");
            fileSystem.AddFile(apocPath, MockFileData.NullObject);

            var sourceMap = new Dictionary<string, string>
            {
                { @"c:\Users\harry\Source\neo\seattle\samples\token-sample\src", srcPath}
            };
            var resolver = new DebugInfo.DocumentResolver(sourceMap, fileSystem);
            var actual = resolver.ResolveDocument(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs");

            Assert.Equal(apocPath, actual);
        }

        [Fact]
        public void resolve_source_via_map_2()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var tokenSamplePath = fileSystem.Path.Combine(rootPath, "neo", "token-sample");
            var apocPath = fileSystem.Path.Combine(tokenSamplePath, "src", "Apoc.cs");
            fileSystem.AddFile(apocPath, MockFileData.NullObject);

            var sourceMap = new Dictionary<string, string>
            {
                { @"c:\Users\harry\Source\neo\seattle\samples\token-sample", tokenSamplePath}
            };
            var resolver = new DebugInfo.DocumentResolver(sourceMap, fileSystem);
            var actual = resolver.ResolveDocument(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs");

            Assert.Equal(apocPath, actual);
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
                s =>
                {
                    Assert.Equal("testStatic1", s.Name);
                    Assert.Equal(PrimitiveContractType.String, s.Type);
                    Assert.Equal(0, s.Index);
                },
                s =>
                {
                    Assert.Equal("testStatic2", s.Name);
                    Assert.Equal(PrimitiveContractType.Hash160, s.Type);
                    Assert.Equal(1, s.Index);
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
                s =>
                {
                    Assert.Equal("testStatic1", s.Name);
                    Assert.Equal(PrimitiveContractType.String, s.Type);
                    Assert.Equal(1, s.Index);
                },
                s =>
                {
                    Assert.Equal("testStatic2", s.Name);
                    Assert.Equal(PrimitiveContractType.Hash160, s.Type);
                    Assert.Equal(3, s.Index);
                });
        }

        [Fact]
        public void throw_format_exception_when_mix_and_match_optional_slot_index()
        {
            var json = new JObject(
                new JProperty("hash", TEST_HASH),
                new JProperty("static-variables",
                    new JArray(
                        "testStatic1,String,1",
                        "testStatic2,Hash160")));

            Assert.Throws<NotSupportedException>(() => DebugInfo.Load(json, t => t.Value<string>()!));
        }

        [Fact]
        public void can_load_debug_info_with_invalid_sequence_points()
        {
            var debugInfoJson = Utility.GetResource("invalidSequencePoints.json");
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
    }
}
#pragma warning restore VSTHRD200