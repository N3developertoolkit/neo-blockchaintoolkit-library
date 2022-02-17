using System.Text.Json;
using Neo.BlockchainToolkit;
using Xunit;
using System.Linq;
using FluentAssertions;
using OneOf;

using FieldType = OneOf.OneOf<Neo.SmartContract.ContractParameterType, Neo.BlockchainToolkit.ContractDataDef.StructDef>;
using ContractParameterType = Neo.SmartContract.ContractParameterType;
using System;
using Neo.BlockchainToolkit.ContractDataDef;

namespace test.bctklib
{
    public class ContractDataDefParserTest
    {
        [Fact]
        public void test_ParseStructs()
        {
            using var doc = ToJsonDoc(new
            {
                SomeTest = new[]
                {
                    new { name = "foo", type = "Hash160" },
                    new { name = "ts", type = "TokenState" }
                },
                TokenState = new[]
                {
                    new { name = "Owner", type = "Hash160" },
                    new { name = "Name", type = "String" },
                    new { name = "Description", type = "String" },
                    new { name = "Image", type = "String" },
                },
            });

            var us = StructDef.ParseStructProp(doc.RootElement);
            var bs = StructDef.BindStructs(us);

            var expect1 = new StructDef("TokenState", new[] {
                    ("Owner", Field(ContractParameterType.Hash160)),
                    ("Name", Field(ContractParameterType.String)),
                    ("Description", Field(ContractParameterType.String)),
                    ("Image", Field(ContractParameterType.String)),
                });

            var expect2 = new StructDef("SomeTest", new[] {
                    ("foo", Field(ContractParameterType.Hash160)),
                    ("ts", FieldType.FromT1(expect1)),
                });

            bs.Should().HaveCount(2)
                .And.Contain(new [] { expect1, expect2 });
        }

        [Fact]
        public void test_invalid_type_throws()
        {
            using var doc = ToJsonDoc(new
            {
                SomeTest = new[]
                {
                    new { name = "foo", type = "Hash160" },
                    new { name = "ts", type = "TokenState" }
                },
            });

            var us = StructDef.ParseStructProp(doc.RootElement);
            Assert.Throws<Exception>(() => StructDef.BindStructs(us));
        }

        static FieldType Field(Neo.SmartContract.ContractParameterType param) => FieldType.FromT0(param);

        static JsonDocument ToJsonDoc(object @struct)
        {
            var json = JsonSerializer.Serialize(new { @struct });
            return JsonDocument.Parse(json);
        }
    }
}
