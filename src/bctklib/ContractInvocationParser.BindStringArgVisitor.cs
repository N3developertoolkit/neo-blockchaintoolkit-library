
using System;
using OneOf;
using None = OneOf.Types.None;

namespace Neo.BlockchainToolkit
{
    public static partial class ContractInvocationParser
    {
        class BindStringArgVisitor : ContractInvocationVisitor<ContractArg>
        {
            readonly Func<string, OneOf<PrimitiveContractArg, None>> tryUpdate;

            public BindStringArgVisitor(Func<string, OneOf<PrimitiveContractArg, None>> tryUpdate)
            {
                this.tryUpdate = tryUpdate;
            }

            public override ContractArg VisitNull(NullContractArg arg) => arg;

            public override ContractArg VisitPrimitive(PrimitiveContractArg arg)
                => arg.Value.TryPickT3(out var @string, out _)
                    ? tryUpdate(@string).Match(updated => updated, _ => arg)
                    : arg;

            public override ContractArg VisitArray(ArrayContractArg arg)
            {
                var values = arg.Values.Update(a => Visit(a) ?? a);
                return ReferenceEquals(arg.Values, values)
                    ? arg
                    : arg with { Values = values };
            }

            public override ContractArg VisitMap(MapContractArg arg)
            {
                var values = arg.Values.Update(
                kvp =>
                {
                    var key = Visit(kvp.key) ?? kvp.key;
                    var value = Visit(kvp.value) ?? kvp.value;
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


