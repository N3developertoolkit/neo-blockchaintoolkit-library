using Xunit;
using Xunit.Extensions;
using System.IO.Abstractions.TestingHelpers;
using Neo.BlockchainToolkit;
using Shouldly;
using Neo.SmartContract;
using System.Collections.Generic;
using Neo;

namespace test.bctklib3
{
    public class ContractParameterParserTest
    {
        // class AccountResolver
        // {
        //     public bool TryGetAccount(string _, [MaybeNullWhen(false)] out UInt160 account)

        // }
        [Theory, MemberData(nameof(StringParameters))]
        public void Foo(string value, ContractParameterType expectedType, object expectedValue)
        {
            var accounts = new Dictionary<string, UInt160>();

            var fileSystem = new MockFileSystem();
            var parser = new ContractParameterParser(fileSystem, accounts.TryGetValue);
            var param = parser.ParseStringParameter(value, string.Empty);
            param.Type.ShouldBe(expectedType);
            param.Value.ShouldBe(expectedValue);
        }



        public static IEnumerable<object[]> StringParameters()
        {
            yield return new object[] { "foo", ContractParameterType.String, "foo" };
            yield return new object[] { "foo", ContractParameterType.Hash160, "foo" };
        }

    }
}
