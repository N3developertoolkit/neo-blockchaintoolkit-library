using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Xunit;

namespace test.bctklib3
{
    public class ExpressChainTest
    {
        [Fact]
        public void missing_address_version_defaults_correctly()
        {
            var fileSystem = new MockFileSystem();
            const string fileName = "c:/default.neo-express";
            fileSystem.AddFile(fileName, new MockFileData("{'magic': 1218031260}"));

            var chain = fileSystem.LoadChain(fileName);
            chain.AddressVersion.Should().Be(ProtocolSettings.Default.AddressVersion);
        }

        [Fact]
        public void specified_address_version_loads_correctly()
        {
            var fileSystem = new MockFileSystem();
            const string fileName = "c:/default.neo-express";
            const byte addressVersion = 0x53;
            fileSystem.AddFile(fileName, new MockFileData($"{{'magic': 1218031260, 'address-version': {addressVersion} }}"));

            var chain = fileSystem.LoadChain(fileName);
            chain.AddressVersion.Should().Be(addressVersion);
        }

    }
}
