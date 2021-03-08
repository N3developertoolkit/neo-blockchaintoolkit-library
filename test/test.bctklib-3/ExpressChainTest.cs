using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Xunit;

namespace test.bctklib3
{
    public class ExpressChainTest
    {
        const string FILENAME = "c:/default.neo-express";
        const string TEST_SETTING = "test.setting";
        const string TEST_SETTING_VALUE = "some value";

        [Fact]
        public void missing_address_version_defaults_correctly()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(FILENAME, new MockFileData("{'magic': 1218031260}"));

            var chain = fileSystem.LoadChain(FILENAME);
            chain.AddressVersion.Should().Be(ProtocolSettings.Default.AddressVersion);
        }

        [Fact]
        public void specified_address_version_loads_correctly()
        {
            const byte addressVersion = 0x53;

            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(FILENAME, new MockFileData($"{{'magic': 1218031260, 'address-version': {addressVersion} }}"));

            var chain = fileSystem.LoadChain(FILENAME);
            chain.AddressVersion.Should().Be(addressVersion);
        }

        [Fact]
        public void settings_default_to_empty_dictionary()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(FILENAME, new MockFileData($"{{ }}"));

            var chain = fileSystem.LoadChain(FILENAME);
            chain.Settings.Should().BeEmpty();
        }

        [Fact]
        public void empty_settings_save_correctly()
        {
            var fileSystem = new MockFileSystem();
            var chain = new ExpressChain();
            fileSystem.SaveChain(chain, FILENAME);

            using var json = JsonDocument.Parse(fileSystem.GetFile(FILENAME).Contents);
            var settings = json.RootElement.GetProperty("settings");
            settings.EnumerateObject().Should().BeEmpty();
        }

        [Fact]
        public void settings_save_correctly()
        {
            var fileSystem = new MockFileSystem();
            var chain = new ExpressChain();
            chain.Settings.Add(TEST_SETTING, TEST_SETTING_VALUE);
            fileSystem.SaveChain(chain, FILENAME);

            using var json = JsonDocument.Parse(fileSystem.GetFile(FILENAME).Contents);
            var settings = json.RootElement.GetProperty("settings");
            settings.EnumerateObject().Should().NotBeEmpty();
            settings.GetProperty(TEST_SETTING).GetString().Should().Be(TEST_SETTING_VALUE);
        }

        [Fact]
        public void settings_load_correctly()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(FILENAME, new MockFileData($"{{'settings': {{ '{TEST_SETTING}': '{TEST_SETTING_VALUE}' }} }}"));

            var chain = fileSystem.LoadChain(FILENAME);

            chain.Settings.Should().Contain(TEST_SETTING, TEST_SETTING_VALUE);
        }
    }
}
