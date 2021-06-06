using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract;
using Xunit;


namespace test.bctklib3
{
    public class ContractParameterParserTest
    {
        readonly byte DEFAULT_ADDRESS_VERSION = Neo.ProtocolSettings.Default.AddressVersion;

        [Fact]
        public void TestParseStringParameter_string()
        {
            const string expected = "string-value";
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);
            var param = parser.ParseStringParameter(expected);
            param.Type.Should().Be(ContractParameterType.String);
            param.Value.Should().Be(expected);
        }

        [Fact]
        public void TestParseStringParameter_empty_string()
        {
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);
            var param = parser.ParseStringParameter(string.Empty);
            param.Type.Should().Be(ContractParameterType.String);
            param.Value.Should().Be(string.Empty);
        }

        [Fact]
        public void TestParseStringParameter_at_account()
        {
            const string account = "test-account";
            var expectedValue = UInt160.Parse("30f41a14ca6019038b055b585d002b287b5fdd47");
            var accounts = new Dictionary<string, UInt160>
            {
                { account, expectedValue }
            };
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION, tryGetAccount: accounts.TryGetValue);
            var param = parser.ParseStringParameter($"@{account}");
            param.Type.Should().Be(ContractParameterType.Hash160);
            param.Value.Should().Be(expectedValue);
        }

        [Fact]
        public void TestParseStringParameter_at_account_missing()
        {
            const string account = "test-account";
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);
            var param = parser.ParseStringParameter($"@{account}");
            param.Type.Should().Be(ContractParameterType.String);
            param.Value.Should().Be($"@{account}");
        }

        [Fact]
        public void TestParseStringParameter_at_address()
        {
            var expectedValue = UInt160.Parse("30f41a14ca6019038b055b585d002b287b5fdd47");
            var address = Neo.Wallets.Helper.ToAddress(expectedValue, DEFAULT_ADDRESS_VERSION);
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);
            var param = parser.ParseStringParameter($"@{address}");
            param.Type.Should().Be(ContractParameterType.Hash160);
            param.Value.Should().Be(expectedValue);
        }


        [Fact]
        public void TestParseStringParameter_at_address_fallthru()
        {
            var uint160 = UInt160.Parse("30f41a14ca6019038b055b585d002b287b5fdd47");
            var address = Neo.Wallets.Helper.ToAddress(uint160, DEFAULT_ADDRESS_VERSION);
            var expected = "@" + address.Substring(0, address.Length - 1);

            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);
            var param = parser.ParseStringParameter(expected);
            param.Type.Should().Be(ContractParameterType.String);
            param.Value.Should().Be(expected);
        }

        [Fact]
        public void TestParseStringParameter_hash_uint160()
        {
            const string hashString = "30f41a14ca6019038b055b585d002b287b5fdd47";
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);
            var param = parser.ParseStringParameter($"#{hashString}");
            param.Type.Should().Be(ContractParameterType.Hash160);
            param.Value.Should().Be(UInt160.Parse(hashString));
        }

        [Fact]
        public void TestParseStringParameter_hash_uint160_fail()
        {
            const string hashString = "#30f41a14ca6019038b055b585d002b287b5fdd4";
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);
            var param = parser.ParseStringParameter(hashString);
            param.Type.Should().Be(ContractParameterType.String);
            param.Value.Should().Be(hashString);
        }

        [Fact]
        public void TestParseStringParameter_hash_uint256()
        {
            const string hashString = "0a372ac8f778eeebb1ccdbb250fe596b83d1d1b9f366d71dfd4c53956bed5cce";
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);
            var param = parser.ParseStringParameter($"#{hashString}");
            param.Type.Should().Be(ContractParameterType.Hash256);
            param.Value.Should().Be(UInt256.Parse(hashString));
        }

        [Fact]
        public void TestParseStringParameter_hash_uint256_fail()
        {
            const string hashString = "#a372ac8f778eeebb1ccdbb250fe596b83d1d1b9f366d71dfd4c53956bed5cce";
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);
            var param = parser.ParseStringParameter(hashString);
            param.Type.Should().Be(ContractParameterType.String);
            param.Value.Should().Be(hashString);
        }

        static string FakeRootPath()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? @"x:\fakepath" : "/fakepath";
        }

        [Fact]
        public void TestParseStringParameter_hash_script()
        {
            const string contractName = "test-contract";
            var expectedValue = UInt160.Parse("30f41a14ca6019038b055b585d002b287b5fdd47");

            var contracts = new Dictionary<string, UInt160>()
            {
                { contractName, expectedValue }
            };

            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION, tryGetContract: contracts.TryGetValue);
            var param = parser.ParseStringParameter($"#{contractName}");
            param.Type.Should().Be(ContractParameterType.Hash160);
            param.Value.Should().Be(expectedValue);
        }

        [Fact]
        public void TestParseStringParameter_hash_script_native_case_match()
        {
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);
            var param = parser.ParseStringParameter("#OracleContract");
            param.Type.Should().Be(ContractParameterType.Hash160);
            param.Value.Should().Be(Neo.SmartContract.Native.NativeContract.Oracle.Hash);
        }

        [Fact]
        public void TestParseStringParameter_hash_script_native_case_mismatch()
        {
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);
            var param = parser.ParseStringParameter("#oraclecontract");
            param.Type.Should().Be(ContractParameterType.Hash160);
            param.Value.Should().Be(Neo.SmartContract.Native.NativeContract.Oracle.Hash);
        }

        [Fact]
        public void TestParseStringParameter_hex_string()
        {
            var expectedValue = new byte[] {
                0x01, 0x02, 0x03, 0x04,
                0x05, 0x06, 0x07, 0x08,
                0x09, 0x0a, 0x0b, 0x0c,
                0x0d, 0x0e, 0x0f, 0x10,
                0x11, 0x12, 0x13, 0x14,
                0x15, 0x16, 0x17, 0x18,
                0x19, 0x1a, 0x1b, 0x1c,
                0x1d, 0x1e, 0x1f, 0x20 };

            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);
            var param = parser.ParseStringParameter($"0x{expectedValue.ToHexString()}");
            param.Type.Should().Be(ContractParameterType.ByteArray);
            param.Value.Should().BeEquivalentTo(expectedValue);
        }

        [Fact]
        public void TestParseStringParameter_file_uri()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var someFakePathFile = fileSystem.Path.Combine(rootPath, "some", "fake", "path", "file.txt");
            var mockFile = new MockFileData("2a333738-c897-45db-ac76-67b66deb4c1f");
            fileSystem.AddFile(someFakePathFile, mockFile);

            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION, fileSystem: fileSystem);
            var param = parser.ParseStringParameter($"file://{someFakePathFile}");
            param.Type.Should().Be(ContractParameterType.ByteArray);
            param.Value.Should().BeEquivalentTo(mockFile.Contents);
        }

        [Fact]
        public void TestParseStringParameter_file_uri_not_found()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var someFakePathFile = fileSystem.Path.Combine(rootPath, "some", "fake", "path", "file.txt");
            var mockFile = new MockFileData("2a333738-c897-45db-ac76-67b66deb4c1f");

            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION, fileSystem: fileSystem);
            var exception = Assert.Throws<FileNotFoundException>(() => parser.ParseStringParameter($"file://{someFakePathFile}"));

            // only check file name due to forward/back slash changes
            Assert.Equal(Path.GetFileName(someFakePathFile), Path.GetFileName(exception.FileName));
        }

        [Fact]
        public void TestParseStringParameter_file_uri_relative()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var someFakePath = fileSystem.Path.Combine(rootPath, "some", "fake", "path");
            fileSystem.Directory.SetCurrentDirectory(someFakePath);
            var mockFile = new MockFileData("2a333738-c897-45db-ac76-67b66deb4c1f");
            fileSystem.AddFile(fileSystem.Path.Combine(someFakePath, "file.txt"), mockFile);

            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION, fileSystem: fileSystem);
            var param = parser.ParseStringParameter($"file://./file.txt");

            param.Type.Should().Be(ContractParameterType.ByteArray);
            param.Value.Should().BeEquivalentTo(mockFile.Contents);
        }

        [Fact]
        public void TestParseStringParameter_file_uri_relative_windows()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var someFakePath = fileSystem.Path.Combine(rootPath, "some", "fake", "path");
            fileSystem.Directory.SetCurrentDirectory(someFakePath);
            var mockFile = new MockFileData("2a333738-c897-45db-ac76-67b66deb4c1f");
            fileSystem.AddFile(fileSystem.Path.Combine(someFakePath, "file.txt"), mockFile);

            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION, fileSystem: fileSystem);
            var param = parser.ParseStringParameter($"file://.\\file.txt");

            param.Type.Should().Be(ContractParameterType.ByteArray);
            param.Value.Should().BeEquivalentTo(mockFile.Contents);
        }

        [Fact]
        public void TestParseStringParameter_file_uri_relative_2_unix()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var someFakePath = fileSystem.Path.Combine(rootPath, "some", "fake", "path");
            fileSystem.Directory.SetCurrentDirectory(fileSystem.Path.GetDirectoryName(someFakePath));
            var mockFile = new MockFileData("2a333738-c897-45db-ac76-67b66deb4c1f");
            fileSystem.AddFile(fileSystem.Path.Combine(someFakePath, "file.txt"), mockFile);

            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION, fileSystem: fileSystem);
            var param = parser.ParseStringParameter($"file://./path/file.txt");
            param.Type.Should().Be(ContractParameterType.ByteArray);
            param.Value.Should().BeEquivalentTo(mockFile.Contents);
        }

        [Fact]
        public void TestParseStringParameter_file_uri_relative_2_windows()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var someFakePath = fileSystem.Path.Combine(rootPath, "some", "fake", "path");
            fileSystem.Directory.SetCurrentDirectory(fileSystem.Path.GetDirectoryName(someFakePath));
            var mockFile = new MockFileData("2a333738-c897-45db-ac76-67b66deb4c1f");
            fileSystem.AddFile(fileSystem.Path.Combine(someFakePath, "file.txt"), mockFile);

            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION, fileSystem: fileSystem);
            var param = parser.ParseStringParameter($"file://.\\path\\file.txt");
            param.Type.Should().Be(ContractParameterType.ByteArray);
            param.Value.Should().BeEquivalentTo(mockFile.Contents);
        }

        [Fact]
        public void TestParseStringParameter_file_uri_relative_3()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var someFakePath = fileSystem.Path.Combine(rootPath, "some", "fake", "path");
            fileSystem.Directory.SetCurrentDirectory(fileSystem.Path.Combine(someFakePath, "test"));
            var mockFile = new MockFileData("2a333738-c897-45db-ac76-67b66deb4c1f");
            fileSystem.AddFile(fileSystem.Path.Combine(someFakePath, "file.txt"), mockFile);

            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION, fileSystem: fileSystem);
            var param = parser.ParseStringParameter($"file://../file.txt");

            param.Type.Should().Be(ContractParameterType.ByteArray);
            param.Value.Should().BeEquivalentTo(mockFile.Contents);
        }

        [Fact]
        public void TestParseStringParameter_file_uri_relative_3_windows()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var someFakePath = fileSystem.Path.Combine(rootPath, "some", "fake", "path");
            fileSystem.Directory.SetCurrentDirectory(fileSystem.Path.Combine(someFakePath, "test"));
            var mockFile = new MockFileData("2a333738-c897-45db-ac76-67b66deb4c1f");
            fileSystem.AddFile(fileSystem.Path.Combine(someFakePath, "file.txt"), mockFile);

            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION, fileSystem: fileSystem);
            var param = parser.ParseStringParameter($"file://..\\file.txt");

            param.Type.Should().Be(ContractParameterType.ByteArray);
            param.Value.Should().BeEquivalentTo(mockFile.Contents);
        }
    }
}
