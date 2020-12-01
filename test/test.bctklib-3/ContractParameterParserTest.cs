using Xunit;
using Xunit.Extensions;
using System.IO.Abstractions.TestingHelpers;
using Neo.BlockchainToolkit;
using Shouldly;
using Neo.SmartContract;
using System.Collections.Generic;
using Neo;
using System;
using Neo.IO;
using System.Runtime.InteropServices;

namespace test.bctklib3
{
    public class ContractParameterParserTest
    {
        [Fact]
        public void TestParseStringParameter_string()
        {
            const string expected = "string-value";
            var fileSystem = new MockFileSystem();
            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter(expected, string.Empty);
            param.Type.ShouldBe(ContractParameterType.String);
            param.Value.ShouldBe(expected);
        }

        [Fact]
        public void TestParseStringParameter_at_account()
        {
            const string account = "test-account";
            var expectedValue = UInt160.Parse("30f41a14ca6019038b055b585d002b287b5fdd47");
            var fileSystem = new MockFileSystem();
            var accounts = new Dictionary<string, UInt160>
            {
                { account, expectedValue }
            };
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter($"@{account}", string.Empty);
            param.Type.ShouldBe(ContractParameterType.Hash160);
            param.Value.ShouldBe(expectedValue);
        }

        [Fact]
        public void TestParseStringParameter_at_account_missing()
        {
            const string account = "test-account";
            var fileSystem = new MockFileSystem();
            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter($"@{account}", string.Empty);
            param.Type.ShouldBe(ContractParameterType.String);
            param.Value.ShouldBe($"@{account}");
        }

        [Fact]
        public void TestParseStringParameter_at_address()
        {
            var expectedValue = UInt160.Parse("30f41a14ca6019038b055b585d002b287b5fdd47");
            var address = Neo.Wallets.Helper.ToAddress(expectedValue);
            var fileSystem = new MockFileSystem();
            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter($"@{address}", string.Empty);
            param.Type.ShouldBe(ContractParameterType.Hash160);
            param.Value.ShouldBe(expectedValue);
        }


        [Fact]
        public void TestParseStringParameter_at_address_fallthru()
        {
            var uint160 = UInt160.Parse("30f41a14ca6019038b055b585d002b287b5fdd47");
            var address = Neo.Wallets.Helper.ToAddress(uint160);
            var expected = "@" + address.Substring(0, address.Length - 1);

            var fileSystem = new MockFileSystem();
            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter(expected, string.Empty);
            param.Type.ShouldBe(ContractParameterType.String);
            param.Value.ShouldBe(expected);
        }

        [Fact]
        public void TestParseStringParameter_hash_uint160()
        {
            const string hashString = "30f41a14ca6019038b055b585d002b287b5fdd47";
            var fileSystem = new MockFileSystem();
            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter($"#{hashString}", string.Empty);
            param.Type.ShouldBe(ContractParameterType.Hash160);
            param.Value.ShouldBe(UInt160.Parse(hashString));
        }

        [Fact]
        public void TestParseStringParameter_hash_uint160_fail()
        {
            const string hashString = "#30f41a14ca6019038b055b585d002b287b5fdd4";
            var fileSystem = new MockFileSystem();
            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter(hashString, string.Empty);
            param.Type.ShouldBe(ContractParameterType.String);
            param.Value.ShouldBe(hashString);
        }

        [Fact]
        public void TestParseStringParameter_hash_uint256()
        {
            const string hashString = "0a372ac8f778eeebb1ccdbb250fe596b83d1d1b9f366d71dfd4c53956bed5cce";
            var fileSystem = new MockFileSystem();
            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter($"#{hashString}", string.Empty);
            param.Type.ShouldBe(ContractParameterType.Hash256);
            param.Value.ShouldBe(UInt256.Parse(hashString));
        }

        [Fact]
        public void TestParseStringParameter_hash_uint256_fail()
        {
            const string hashString = "#a372ac8f778eeebb1ccdbb250fe596b83d1d1b9f366d71dfd4c53956bed5cce";
            var fileSystem = new MockFileSystem();
            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter(hashString, string.Empty);
            param.Type.ShouldBe(ContractParameterType.String);
            param.Value.ShouldBe(hashString);
        }

        static string FakeRootPath()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? @"x:\fakepath" : "/fakepath";
        }

        [Fact]
        public void TestParseStringParameter_hash_script_absolute()
        {
            var nef = new NefFile
            {
                Compiler = "".PadLeft(32, ' '),
                Version = new Version(1, 2, 3, 4),
                Script = new byte[] { 0x01, 0x02, 0x03 }
            };
            nef.ScriptHash = nef.Script.ToScriptHash();
            nef.CheckSum = NefFile.ComputeChecksum(nef);

            var fileSystem = new MockFileSystem();
            var nefPath = fileSystem.Path.Combine(FakeRootPath(), "contract.nef");
            fileSystem.AddFile(nefPath, new MockFileData(nef.ToArray()));

            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter($"#{nefPath}", string.Empty);
            param.Type.ShouldBe(ContractParameterType.Hash160);
            param.Value.ShouldBe(nef.ScriptHash);
        }

        [Fact]
        public void TestParseStringParameter_hash_script_relative()
        {
            var nef = new NefFile
            {
                Compiler = "".PadLeft(32, ' '),
                Version = new Version(1, 2, 3, 4),
                Script = new byte[] { 0x01, 0x02, 0x03 }
            };
            nef.ScriptHash = nef.Script.ToScriptHash();
            nef.CheckSum = NefFile.ComputeChecksum(nef);

            var fileSystem = new MockFileSystem();
            var rootPath = FakeRootPath();
            var nefPath = fileSystem.Path.Combine(rootPath, "contract.nef");
            fileSystem.AddFile(nefPath, new MockFileData(nef.ToArray()));

            var relativePath = fileSystem.Path.GetRelativePath(rootPath, nefPath);

            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter($"#{relativePath}", rootPath);
            param.Type.ShouldBe(ContractParameterType.Hash160);
            param.Value.ShouldBe(nef.ScriptHash);
        }

        [Fact]
        public void TestParseStringParameter_hash_script_relative_invalid()
        {
            const string nefPath = @"x:\fakepath\contract.nef";
            var nef = new NefFile
            {
                Compiler = "".PadLeft(32, ' '),
                Version = new Version(1, 2, 3, 4),
                Script = new byte[] { 0x01, 0x02, 0x03 }
            };
            nef.ScriptHash = nef.Script.ToScriptHash();
            nef.CheckSum = NefFile.ComputeChecksum(nef);

            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(nefPath, new MockFileData(nef.ToArray()));

            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);

            var param = parser.ParseStringParameter("#contract.nef", string.Empty);
            param.Type.ShouldBe(ContractParameterType.String);
            param.Value.ShouldBe("#contract.nef");
        }

        [Fact]
        public void TestParseStringParameter_hash_script_native()
        {
            var fileSystem = new MockFileSystem();
            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter("#oracle", string.Empty);
            param.Type.ShouldBe(ContractParameterType.Hash160);
            param.Value.ShouldBe(Neo.SmartContract.Native.NativeContract.Oracle.Hash);
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

            var fileSystem = new MockFileSystem();
            var accounts = new Dictionary<string, UInt160>();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter($"0x{expectedValue.ToHexString()}", string.Empty);
            param.Type.ShouldBe(ContractParameterType.ByteArray);
            param.Value.ShouldBe(expectedValue);
        }
    }
}
