
using System;
using System.Collections.Generic;
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

            public T RecordError<T>(string message, T value)
            {
                recordError(message);
                return value;
            }

            public override ContractArg VisitNull(NullContractArg arg) => arg;

            public override ContractArg VisitPrimitive(PrimitiveContractArg arg)
                => arg.Value.TryPickT3(out var @string, out _)
                    ? bindingFunc(@string, recordError).Match(updated => updated, _ => arg)
                    : arg;

            public override ContractArg VisitArray(ArrayContractArg arg)
            {
                var values = arg.Values.Update(a => Visit(a) ?? RecordError("null visitor return", a));
                return ReferenceEquals(arg.Values, values)
                    ? arg
                    : arg with { Values = values };
            }

            public override ContractArg VisitMap(MapContractArg arg)
            {
                var values = arg.Values.Update(
                kvp =>
                {
                    var key = Visit(kvp.key) ?? RecordError("null visitor return", kvp.key);
                    var value = Visit(kvp.value) ?? RecordError("null visitor return", kvp.value);
                    return ReferenceEquals(kvp.key, key)
                        && ReferenceEquals(kvp.value, value)
                            ? kvp
                            : (key, value);
                },
                (a, b) => ReferenceEquals(a.key, b.key)
                    && ReferenceEquals(a.value, b.value));

                return object.ReferenceEquals(arg.Values, values)
                    ? arg
                    : arg with { Values = values };
            }
        }
    }
}


