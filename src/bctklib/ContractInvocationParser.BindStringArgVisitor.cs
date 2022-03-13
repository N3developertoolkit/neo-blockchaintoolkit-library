
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using OneOf;
using None = OneOf.Types.None;

namespace Neo.BlockchainToolkit
{
    using BindingFunc = Func<string, Action<string>, OneOf<PrimitiveContractArg, OneOf.Types.None>>;

    public static partial class ContractInvocationParser
    {
        class BindStringArgVisitor : ContractInvocationVisitor<ContractArg>
        {
            readonly BindingFunc bindingFunc;
            readonly Action<string> recordError;

            public BindStringArgVisitor(BindingFunc bindingFunc, Action<string> recordError)
            {
                this.bindingFunc = bindingFunc;
                this.recordError = recordError;
            }

            public override ContractArg VisitNull(NullContractArg arg) => arg;

            public override ContractArg VisitPrimitive(PrimitiveContractArg arg)
                => arg.Value.TryPickT3(out var @string, out _)
                    ? bindingFunc(@string, recordError).Match(updated => updated, _ => arg)
                    : arg;

            public ContractArg NullCheckVisit(ContractArg arg)
            {
                var result = Visit(arg);
                if (result is null)
                {
                    recordError("null visitor return");
                    return arg;
                }
                return result;
            }

            public override ContractArg VisitArray(ArrayContractArg arg)
            {
                var values = arg.Values.Update(NullCheckVisit);
                return ReferenceEquals(arg.Values, values)
                    ? arg
                    : arg with { Values = values };
            }

            public override ContractArg VisitMap(MapContractArg arg)
            {
                var values = arg.Values.Update(
                    kvp =>
                    {
                        var key = NullCheckVisit(kvp.key);
                        var value = NullCheckVisit(kvp.value);

                        return ReferenceEquals(kvp.key, key)
                            && ReferenceEquals(kvp.value, value)
                                ? kvp
                                : (key, value);
                    },
                    (a, b) =>
                        ReferenceEquals(a.key, b.key)
                        && ReferenceEquals(a.value, b.value));

                return ReferenceEquals(arg.Values, values)
                    ? arg
                    : arg with { Values = values };
            }
        }
    }
}
