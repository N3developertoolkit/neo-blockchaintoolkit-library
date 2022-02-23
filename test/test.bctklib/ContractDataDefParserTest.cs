using System;
using System.Linq;
using FluentAssertions;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace test.bctklib
{
    public class ContractDataDefParserTest
    {
        // [Fact]
        // public void test_ParseStructs()
        // {
        //     var json = JObject.FromObject(new
        //     {
        //         SomeTest = new[]
        //         {
        //             new { name = "foo", type = "Hash160" },
        //             new { name = "ts", type = "TokenState" }
        //         },
        //         TokenState = new[]
        //         {
        //             new { name = "Owner", type = "Hash160" },
        //             new { name = "Name", type = "String" },
        //             new { name = "Description", type = "String" },
        //             new { name = "Image", type = "String" },
        //         },
        //     });

        //     var structs = StructDef.ParseStructDefs(json);

        //     // var expect1 = new StructDef("TokenState", new[] {
        //     //         ("Owner", Field(ContractParameterType.Hash160)),
        //     //         ("Name", Field(ContractParameterType.String)),
        //     //         ("Description", Field(ContractParameterType.String)),
        //     //         ("Image", Field(ContractParameterType.String)),
        //     //     });

        //     // var expect2 = new StructDef("SomeTest", new[] {
        //     //         ("foo", Field(ContractParameterType.Hash160)),
        //     //         ("ts", FieldType.FromT1(expect1)),
        //     //     });

        //     // structs.Should().HaveCount(2)
        //     //     .And.Contain(new[] { expect1, expect2 });
        // }

        // [Fact]
        // public void test_invalid_type_throws()
        // {
        //     var json = JObject.FromObject(new
        //     {
        //         SomeTest = new[]
        //         {
        //             new { name = "foo", type = "Hash160" },
        //             new { name = "ts", type = "TokenState" }
        //         },
        //     });

        //     Assert.Throws<Exception>(() => StructDef.ParseStructDefs(json));
        // }

        [Fact]
        public void test_key_prefix_single_byte()
        {
            var keyJson = new JObject(new JProperty("prefix", 42));
            var prefix = ContractStorageSchema.ParseKeyPrefix(keyJson);
            prefix.Span.SequenceEqual(new byte[] { 42 }).Should().BeTrue();
        }

        [Fact]
        public void test_key_prefix_invalid_single_byte()
        {
            var keyJson = new JObject(new JProperty("prefix", 1000));
            Assert.Throws<OverflowException>(() => ContractStorageSchema.ParseKeyPrefix(keyJson));
        }

        [Fact]
        public void test_key_prefix_invalid_byte_in_array()
        {
            var keyJson = new JObject(new JProperty("prefix", new JArray(42, 1000, 48)));
            Assert.Throws<OverflowException>(() => ContractStorageSchema.ParseKeyPrefix(keyJson));
        }

        [Fact]
        public void test_key_prefix_byte_array()
        {
            var keyJson = new JObject(new JProperty("prefix", new JArray(42, 48)));
            var prefix = ContractStorageSchema.ParseKeyPrefix(keyJson);
            prefix.Span.SequenceEqual(new byte[] { 42, 48 }).Should().BeTrue();
        }

        [Fact]
        public void test_key_prefix_string()
        {
            var keyJson = new JObject(new JProperty("prefix", "test"));
            var prefix = ContractStorageSchema.ParseKeyPrefix(keyJson);
            prefix.Span.SequenceEqual(new byte[] { 0x74, 0x65, 0x73, 0x74 }).Should().BeTrue();
        }

        // [Fact]
        // public void test_ParseKeySegments_segment_object()
        // {
        //     var segmentsJson = new JObject(
        //         new JProperty("segments", 
        //             JObject.FromObject(new
        //                 {
        //                     name = "owner",
        //                     type = "Hash160"
        //                 })));
        //     var segments = StorageDef.ParseKeySegments(segmentsJson);
        //     segments.Should().HaveCount(1)
        //         .And.Contain(new StorageDef.KeySegment("owner", PrimitiveStorageType.Hash160));
        // }

        // [Fact]
        // public void test_ParseKeySegments_segment_array()
        // {
        //     var segmentsJson = new JObject(
        //         new JProperty("segments", 
        //             new JArray(
        //                 JObject.FromObject(new { name = "owner", type = "Hash160" }),
        //                 JObject.FromObject(new { name = "tokenId", type = "Hash256" })
        //             )));
        //     var segments = StorageDef.ParseKeySegments(segmentsJson);
        //     segments.Should().HaveCount(2)
        //         .And.Contain(new StorageDef.KeySegment("owner", PrimitiveStorageType.Hash160))
        //         .And.Contain(new StorageDef.KeySegment("tokenId", PrimitiveStorageType.Hash256));
        // }

        // [Fact(Skip = "Need To Update")]
        // public void test_parse_storage()
        // {
        //     var tokenStateDef = new StructDef("TokenState", new[] {
        //         ("Owner", Field(PrimitiveStorageType.Hash160)),
        //         ("Name", Field(PrimitiveStorageType.String)),
        //         ("Description", Field(PrimitiveStorageType.String)),
        //         ("Image", Field(PrimitiveStorageType.String)),
        //     });

        //     var structMap = new[] { tokenStateDef }.ToDictionary(s => s.Name);

        //     var json = JObject.FromObject(
        //         new
        //         {
        //             TotalSupply = new
        //             {
        //                 key = new JArray(0),
        //                 value = "Integer"
        //             },
        //             Balance = new
        //             {
        //                 key = new JArray(1, JObject.FromObject(new
        //                 {
        //                     name = "owner",
        //                     type = "Hash160"
        //                 })),
        //                 value = "Integer"
        //             },
        //             TokenId = new
        //             {
        //                 key = new JArray(2),
        //                 value = "Integer"
        //             },
        //             Token = new
        //             {
        //                 key = new JArray(3, JObject.FromObject(new
        //                 {
        //                     name = "tokenId",
        //                     type = "Hash256"
        //                 })),
        //                 value = "TokenState",
        //             },
        //             AccountToken = new
        //             {
        //                 key = new JArray(
        //                 4,
        //                 JObject.FromObject(new
        //                 {
        //                     name = "owner",
        //                     type = "Hash160"
        //                 }),
        //                 JObject.FromObject(new
        //                 {
        //                     name = "tokenId",
        //                     type = "Hash256"
        //                 })
        //             ),
        //                 value = "Integer",
        //             },
        //             ContractOwner = new
        //             {
        //                 key = new JArray(0xff),
        //                 value = "Hash160",
        //             }
        //         });

        //     var storageDefs = StorageDef.ParseStorageDefs(json, structMap);
        //     storageDefs.Should().HaveCount(6);
        // }

        // [Fact(Skip = "Need To Update")]
        // public void test_contract_schema_parse()
        // {
        //     var json = JObject.FromObject(new
        //     {
        //         @struct = new
        //         {
        //             SomeTest = new[]
        //             {
        //                 new { name = "foo", type = "Hash160" },
        //                 new { name = "ts", type = "TokenState" }
        //             },
        //             TokenState = new[]
        //             {
        //                 new { name = "Owner", type = "Hash160" },
        //                 new { name = "Name", type = "String" },
        //                 new { name = "Description", type = "String" },
        //                 new { name = "Image", type = "String" },
        //             },
        //         },
        //         storage = new
        //         {
        //             TotalSupply = new
        //             {
        //                 key = new JArray(0),
        //                 value = "Integer"
        //             },
        //             Balance = new
        //             {
        //                 key = new JArray(1, JObject.FromObject(new
        //                 {
        //                     name = "owner",
        //                     type = "Hash160"
        //                 })),
        //                 value = "Integer"
        //             },
        //             TokenId = new
        //             {
        //                 key = new JArray(2),
        //                 value = "Integer"
        //             },
        //             Token = new
        //             {
        //                 key = new JArray(3, JObject.FromObject(new
        //                 {
        //                     name = "tokenId",
        //                     type = "Hash256"
        //                 })),
        //                 value = "TokenState",
        //             },
        //             AccountToken = new
        //             {
        //                 key = new JArray(
        //                     4,
        //                     JObject.FromObject(new
        //                     {
        //                         name = "owner",
        //                         type = "Hash160"
        //                     }),
        //                     JObject.FromObject(new
        //                     {
        //                         name = "tokenId",
        //                         type = "Hash256"
        //                     })),
        //                 value = "Integer",
        //             },
        //             ContractOwner = new
        //             {
        //                 key = new JArray(0xff),
        //                 value = "Hash160",
        //             }
        //         }
        //     });

        //     var schema = ContractStorageSchema.Parse(json);
        //     schema.StructDefs.Should().HaveCount(2);
        //     schema.StorageDefs.Should().HaveCount(6);
        // }

        // static FieldType Field(PrimitiveStorageType param) => FieldType.FromT0(param);
    }
}
