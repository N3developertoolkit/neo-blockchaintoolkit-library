using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Models;
using Xunit;


namespace test.bctklib3
{
    public class DebugInfoTest
    {
        const string DEBUG_INFO = @"{""hash"":""0xbda6737a03e308a4b024aa92fbc6587b4dd2c337"",""documents"":[""c:\\Users\\harry\\Source\\neo\\seattle\\samples\\token-sample\\src\\Apoc.Crowdsale.cs"",""c:\\Users\\harry\\Source\\neo\\seattle\\samples\\token-sample\\src\\Apoc.cs""],""methods"":[{""id"":""DevHawk.Contracts.ApocToken.OnPayment(Neo.UInt160, System.Numerics.BigInteger, object)"",""name"":""DevHawk.Contracts.ApocToken,OnPayment"",""range"":""0-203"",""params"":[""from,Hash160"",""amount,Integer"",""data,Any""],""return"":""Void"",""variables"":[],""sequence-points"":[""3[0]13:9-13:10"",""4[0]14:17-14:48"",""14[0]15:13-15:14""]},{""id"":""DevHawk.Contracts.AssetStorage.GetPaymentStatus()"",""name"":""DevHawk.Contracts.AssetStorage,GetPaymentStatus"",""range"":""204-249"",""params"":[],""return"":""Boolean"",""variables"":[],""sequence-points"":[""204[1]31:9-31:10"",""205[1]32:13-32:106"",""249[1]33:9-33:10""]}],""events"":[{""id"":""Transfer"",""name"":""DevHawk.Contracts.ApocToken,OnTransfer"",""params"":[""arg1,Hash160"",""arg2,Hash160"",""arg3,Integer""]}]}";

        [Fact]
        public async Task can_load_debug_json()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\fakeContract.nef", new MockFileData("") },
                { @"c:\fakeContract.debug.json", new MockFileData(DEBUG_INFO) },
            });
            var debugInfo = await DebugInfo.LoadAsync(@"c:\fakeContract.nef", null, fileSystem);
            Assert.True(debugInfo.IsT0);
        }

        static MockFileData CreateCompressedDebugInfo(string contractName, string debugInfo)
        {
            var jsonDebugInfo = Neo.IO.Json.JObject.Parse(debugInfo);
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                using var stream = archive.CreateEntry($"{contractName}.debug.json").Open();
                stream.Write(jsonDebugInfo.ToByteArray(false));
            }
            return new MockFileData(memoryStream.ToArray());
        }

        [Fact]
        public async Task can_load_nefdbgnfo()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\fakeContract.nef", new MockFileData("") },
                { @"c:\fakeContract.nefdbgnfo", CreateCompressedDebugInfo("fakeContract", DEBUG_INFO) },
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
        public async Task resolve_source_current_directory()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\fakeContract.nef", new MockFileData("") },
                { @"c:\fakeContract.debug.json", new MockFileData(DEBUG_INFO) },
                { @"c:\src\Apoc.cs", new MockFileData("") },
                { @"c:\src\Apoc.Crowdsale.cs", new MockFileData("") },
            }, @"c:\src");
            var debugInfo = await DebugInfo.LoadAsync(@"c:\fakeContract.nef", null, fileSystem);
            Assert.True(debugInfo.IsT0);

            Assert.Collection(debugInfo.AsT0.Documents,
                d => Assert.Equal(@"c:\src\Apoc.Crowdsale.cs", d),
                d => Assert.Equal(@"c:\src\Apoc.cs", d));
        }

        [Fact]
        public async Task resolve_source_files_exist()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\fakeContract.nef", new MockFileData("") },
                { @"c:\fakeContract.debug.json", new MockFileData(DEBUG_INFO) },
                { @"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs", new MockFileData("") },
                { @"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.Crowdsale.cs", new MockFileData("") },
            }, @"c:\src");
            var debugInfo = await DebugInfo.LoadAsync(@"c:\fakeContract.nef", null, fileSystem);
            Assert.True(debugInfo.IsT0);

            Assert.Collection(debugInfo.AsT0.Documents,
                d => Assert.Equal(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.Crowdsale.cs", d),
                d => Assert.Equal(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs", d));
        }

        [Fact]
        public async Task resolve_source_files_dont_exist()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\fakeContract.nef", new MockFileData("") },
                { @"c:\fakeContract.debug.json", new MockFileData(DEBUG_INFO) },
            }, @"c:\src");
            var debugInfo = await DebugInfo.LoadAsync(@"c:\fakeContract.nef", null, fileSystem);
            Assert.True(debugInfo.IsT0);

            Assert.Collection(debugInfo.AsT0.Documents,
                d => Assert.Equal(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.Crowdsale.cs", d),
                d => Assert.Equal(@"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs", d));
        }

        [Fact]
        public async Task resolve_source_via_map()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { @"c:\fakeContract.nef", new MockFileData("") },
                { @"c:\fakeContract.debug.json", new MockFileData(DEBUG_INFO) },
                { @"c:\src\Apoc.cs", new MockFileData("") },
                { @"c:\src\Apoc.Crowdsale.cs", new MockFileData("") },
            });

            var sourceMap = new Dictionary<string, string>
            {
                { @"c:\Users\harry\Source\neo\seattle\samples\token-sample\src", @"c:\src"}
            };

            var debugInfo = await DebugInfo.LoadAsync(@"c:\fakeContract.nef", sourceMap, fileSystem);
            Assert.True(debugInfo.IsT0);

            Assert.Collection(debugInfo.AsT0.Documents,
                d => Assert.Equal(@"c:\src\Apoc.Crowdsale.cs", d),
                d => Assert.Equal(@"c:\src\Apoc.cs", d));
        }
    }
}
