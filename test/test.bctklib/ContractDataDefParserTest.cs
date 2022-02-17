using System;
using System.Linq;
using FluentAssertions;
using Neo.BlockchainToolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using FieldType = OneOf.OneOf<Neo.SmartContract.ContractParameterType, Neo.BlockchainToolkit.StructDef>;
using ContractParameterType = Neo.SmartContract.ContractParameterType;

namespace test.bctklib
{
    public class ContractDataDefParserTest
    {
        [Fact]
        public void test_ParseStructs()
        {
            var json = JObject.FromObject(new
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

            var structs = StructDef.ParseStructDefs(json);

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

            structs.Should().HaveCount(2)
                .And.Contain(new[] { expect1, expect2 });
        }

        [Fact]
        public void test_invalid_type_throws()
        {
            var json = JObject.FromObject(new
            {
                SomeTest = new[]
                {
                    new { name = "foo", type = "Hash160" },
                    new { name = "ts", type = "TokenState" }
                },
            });

            Assert.Throws<Exception>(() => StructDef.ParseStructDefs(json));
        }

        [Fact]
        public void test_parse_storage()
        {
            var tokenStateDef = new StructDef("TokenState", new[] {
                ("Owner", Field(ContractParameterType.Hash160)),
                ("Name", Field(ContractParameterType.String)),
                ("Description", Field(ContractParameterType.String)),
                ("Image", Field(ContractParameterType.String)),
            });

            var structMap = new[] { tokenStateDef }.ToDictionary(s => s.Name);

            var json = JObject.FromObject(
                new
                {
                    TotalSupply = new
                    {
                        key = new JArray(0),
                        value = "Integer"
                    },
                    Balance = new
                    {
                        key = new JArray(1, JObject.FromObject(new
                        {
                            name = "owner",
                            type = "Hash160"
                        })),
                        value = "Integer"
                    },
                    TokenId = new
                    {
                        key = new JArray(2),
                        value = "Integer"
                    },
                    Token = new
                    {
                        key = new JArray(3, JObject.FromObject(new
                        {
                            name = "tokenId",
                            type = "Hash256"
                        })),
                        value = "TokenState",
                    },
                    AccountToken = new
                    {
                        key = new JArray(
                        4,
                        JObject.FromObject(new
                        {
                            name = "owner",
                            type = "Hash160"
                        }),
                        JObject.FromObject(new
                        {
                            name = "tokenId",
                            type = "Hash256"
                        })
                    ),
                        value = "Integer",
                    },
                    ContractOwner = new
                    {
                        key = new JArray(0xff),
                        value = "Hash160",
                    }
                });

            var storageDefs = StorageDef.ParseStorageDefs(json, structMap);
            storageDefs.Should().HaveCount(6);
        }

        static FieldType Field(Neo.SmartContract.ContractParameterType param) => FieldType.FromT0(param);
    }
}
