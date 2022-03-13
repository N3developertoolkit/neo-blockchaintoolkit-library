
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using OneOf;

namespace Neo.BlockchainToolkit
{
    public abstract class ContractInvocationVisitor<TResult>
    {
        [return: MaybeNull]
        public virtual TResult Visit(ContractArg arg)
        {
            return arg.Accept(this);
        }

        [return: MaybeNull]
        public virtual TResult VisitNull(NullContractArg arg) 
        {
            return default;
        }

        [return: MaybeNull]
        public virtual TResult VisitPrimitive(PrimitiveContractArg arg)
        {
            return default;
        }

        [return: MaybeNull]
        public virtual TResult VisitArray(ArrayContractArg arg)
        {
            return default;
        }

        [return: MaybeNull]
        public virtual TResult VisitMap(MapContractArg arg)
        {
            return default;
        }
    }
}


