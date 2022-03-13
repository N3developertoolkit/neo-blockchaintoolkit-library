
using System;
using System.Collections.Generic;
using System.Numerics;
using OneOf;

namespace Neo.BlockchainToolkit
{
    public abstract class ContractInvocationVisitor<TResult>
    {
        public virtual TResult Visit(ContractArg arg)
        {
            return arg.Accept(this);
        }

        public virtual TResult VisitNull(NullContractArg arg) 
        {
            return default!;
        }

        public virtual TResult VisitPrimitive(PrimitiveContractArg arg)
        {
            return default!;
        }

        public virtual TResult VisitArray(ArrayContractArg arg)
        {
            return default!;
        }

        public virtual TResult VisitMap(MapContractArg arg)
        {
            return default!;
        }
    }
}


