using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using Xunit;
using FluentAssertions;

namespace test.bctklib
{
    public class CodeCoverageTests : IClassFixture<DeployedContractFixture>
    {
        const string COVERAGE_ENV_NAME = "NEO_TEST_APP_ENGINE_COVERAGE_PATH";

        readonly DeployedContractFixture deployedContractFixture;
        readonly Script script;

        public CodeCoverageTests(DeployedContractFixture deployedContractFixture)
        {
            this.deployedContractFixture = deployedContractFixture;

            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(deployedContractFixture.ContractHash, "query", CallFlags.All, "test.domain");
            script = builder.ToArray();
        }

        [Fact]
        public void Coverage_Generated()
        {
            var fs = new MockFileSystem();
            var coveragePath = fs.Path.Combine(fs.Path.GetTempPath(), fs.Path.GetRandomFileName());
            System.Environment.SetEnvironmentVariable(COVERAGE_ENV_NAME, coveragePath);

            using var snapshot = new SnapshotCache(deployedContractFixture.Store);
            using var engine = new TestApplicationEngine(snapshot, fileSystem: fs);

            engine.LoadScript(script);
            var result = engine.Execute();

            var scriptPath = fs.Path.Combine(coveragePath, "0xb127f874f7c5a287148f9b35baa43f147f271dba.neo-script");
            fs.FileExists(scriptPath).Should().BeTrue();

            var nefPath = fs.Path.Combine(coveragePath, "0xe8c2cf8b50016c94f6eafbdd024febc6cd0672fe.nef");
            fs.FileExists(nefPath).Should().BeTrue();

            fs.AllFiles
                .Where(f => fs.Path.GetExtension(f) == ".neo-coverage")
                .Count().Should().Be(1);
        }
    }
}
