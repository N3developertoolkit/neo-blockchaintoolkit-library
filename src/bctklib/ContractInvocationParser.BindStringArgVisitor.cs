
using System;

namespace Neo.BlockchainToolkit
{
    public static partial class ContractInvocationParser
    {
        class BindStringArgVisitor : ContractInvocationVisitor<ContractArg>
        {
            readonly Func<PrimitiveContractArg, PrimitiveContractArg> update;

            public BindStringArgVisitor(Func<PrimitiveContractArg, PrimitiveContractArg> update)
            {
                this.update = update;
            }

            public override ContractArg VisitNull(NullContractArg arg) => arg;

            public override ContractArg VisitPrimitive(PrimitiveContractArg arg) => update(arg);

            public override ContractArg VisitArray(ArrayContractArg arg)
            {
                var values = arg.Values.Update(Visit);
                return ReferenceEquals(arg.Values, values)
                    ? arg
                    : arg with { Values = values };
            }

            public override ContractArg VisitMap(MapContractArg arg)
            {
                var values = arg.Values.Update(
                kvp =>
                {
                    var key = Visit(kvp.key);
                    var value = Visit(kvp.value);
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


