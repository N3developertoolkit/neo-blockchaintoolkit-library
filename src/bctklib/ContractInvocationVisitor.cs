
using System;
using System.Collections.Generic;
using System.Numerics;
using OneOf;

namespace Neo.BlockchainToolkit
{
    public abstract class ContractInvocationVisitor
    {
        public virtual void Visit(IEnumerable<ContractInvocation> invocations)
        {
            if (invocations is IReadOnlyList<ContractInvocation> invocationList)
            {
                for (int i = 0; i < invocationList.Count; i++)
                {
                    invocationList[i].Accept(this);
                }
            }
            else
            {
                foreach (var invocation in invocations)
                {
                    invocation.Accept(this);
                }
            }
        }

        public virtual void Visit(ContractInvocation invocation)
        {
            for (int i = 0; i < invocation.Args.Count; i++)
            {
                invocation.Args[i].Accept(this);
            }
        }

        public virtual void VisitNull(NullContractArg arg) {}

        public virtual void VisitPrimitive(PrimitiveContractArg arg) {}

        public virtual void VisitArray(ArrayContractArg arg)
        {
            for (int i = 0; i < arg.Values.Count; i++) 
            {
                arg.Values[i].Accept(this); 
            }
        }

        public virtual void VisitMap(MapContractArg arg)
        {
            for (int i = 0; i < arg.Values.Count; i++)
            {
                arg.Values[i].key.Accept(this);
                arg.Values[i].value.Accept(this);
            }
        }
    }
}


