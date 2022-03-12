
using System;
using System.Collections.Generic;
using System.Numerics;
using OneOf;

namespace Neo.BlockchainToolkit
{
    public static partial class ContractInvocationParser
    {
        class ValidationVistor : ContractInvocationVisitor
        {
            bool isValid = true;
            readonly ICollection<Diagnostic> diagnostics;

            public bool IsValid => isValid;

            public ValidationVistor(ICollection<Diagnostic> diagnostics)
            {
                this.diagnostics = diagnostics;
            }

            public override void Visit(ContractInvocation invocation)
            {
                base.Visit(invocation);
                if (invocation.Contract.IsT1)
                {
                    diagnostics.Add(Diagnostic.Error($"Unbound contract hash {invocation.Contract.AsT1}"));
                    isValid = false;
                }
            }

            public override void VisitPrimitive(PrimitiveContractArg arg)
            {
                base.VisitPrimitive(arg);
                if (arg.Value.IsT3)
                {
                    diagnostics.Add(Diagnostic.Error($"Unbound string arg '{arg.Value.AsT3}"));
                    isValid = false;
                }
            }

            public override void VisitMap(MapContractArg arg)
            {
                base.VisitMap(arg);
                foreach (var kvp in arg.Values)
                {
                    if (kvp.key is not PrimitiveContractArg)
                    {
                        diagnostics.Add(Diagnostic.Error("Map keys must be primitive types"));
                        isValid = false;
                    }
                }
            }
        }
    }
}


