using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Neo.BlockchainToolkit.Models;

namespace Neo.BlockchainToolkit
{
    public static partial class ContractInvocationParser
    {
        class ValidationVisitor : ContractInvocationVisitor<bool>
        {
            bool isValid = true;
            readonly ICollection<Diagnostic> diagnostics;

            public bool IsValid => isValid;

            public ValidationVisitor(ICollection<Diagnostic> diagnostics)
            {
                this.diagnostics = diagnostics;
            }

            public override bool VisitNull(NullContractArg arg) => true;

            public override bool VisitPrimitive(PrimitiveContractArg arg)
            {
                if (arg.Value.TryPickT3(out var @string, out _))
                {
                    diagnostics.Add(Diagnostic.Error($"Unbound string arg '{@string}"));
                    return false;
                }
                return true;
            }

            public override bool VisitArray(ArrayContractArg arg)
            {
                bool valid = true;
                for (int i = 0; i < arg.Values.Count; i++)
                {
                    valid &= Visit(arg.Values[i]);
                }
                return valid;
            }

            public override bool VisitMap(MapContractArg arg)
            {
                bool valid = true;
                for (int i = 0; i < arg.Values.Count; i++)
                {
                    var kvp = arg.Values[i];
                    valid &= Visit(kvp.key);
                    valid &= Visit(kvp.value);
                    if (kvp.key is not PrimitiveContractArg)
                    {
                        diagnostics.Add(Diagnostic.Error("Map keys must be primitive types"));
                        valid = false;
                    }
                }
                return valid;
            }
        }
    }
}


