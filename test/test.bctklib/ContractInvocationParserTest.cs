using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

using Neo;
using Neo.BlockchainToolkit;
using Newtonsoft.Json.Linq;
using Xunit;
using System.Text.Json;
using System.Numerics;

namespace test.bctklib
{
    public class ContractInvocationParserTest
    {
        class Converter
        {
            readonly IReadOnlyList<(string text, UInt160 hash)> items;

            public Converter(IReadOnlyList<(string text, UInt160 hash)> items)
            {
                this.items = items;
            }

            public Converter(params (string text, UInt160 hash)[] items)
            {
                this.items = items;
            }

            public bool Convert(string text, [MaybeNullWhen(false)] out UInt160 hash)
            {
                for (int i1 = 0; i1 < items.Count; i1++)
                {
                    (string text, UInt160 hash) i = items[i1];
                    if (i.text.Equals(text))
                    {
                        hash = i.hash;
                        return true;
                    }
                }

                for (int i1 = 0; i1 < items.Count; i1++)
                {
                    (string text, UInt160 hash) i = items[i1];
                    if (i.text.Equals(text, StringComparison.OrdinalIgnoreCase))
                    {
                        hash = i.hash;
                        return true;
                    }
                }

                hash = UInt160.Zero;
                return false;
            }

        }

        [Fact]
        public void TestName()
        {
            var doc = JsonDocument.Parse("18446744073709551615");
            var a1 = doc.RootElement.TryGetInt64(out var @long);
            var a2 = doc.RootElement.TryGetUInt64(out var @ulong);
            var a3 = doc.RootElement.TryGetDecimal(out var @decimal);
            var a4 = BigInteger.Parse(doc.RootElement.GetRawText());
            ;
        }
        // [Fact]
        // public void TestName()
        // {
        //     var invokeJson = Utility.GetResourceJson("buy-listed-nft.neo-invoke.json");
        //     var invoke = ContractInvocationParser.ParseInvocation((JObject)invokeJson.First!);

        //     List<Diagnostic> diags = new();
        //     Action<string> reportError = msg => diags.Add(Diagnostic.Error(msg));

        //     var contractConverter = new Converter(("DemoShopContract", UInt160.Zero));
        //     invoke = ContractInvocationParser.BindContract(invoke, contractConverter.Convert, reportError);

        //     ;
        //     invoke = ContractInvocationParser.BindContractArgs(invoke, contractConverter.Convert, reportError);

        //     ;

        //     // When

        //     // Then
        // }

    }
}
