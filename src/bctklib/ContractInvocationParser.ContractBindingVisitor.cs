// using System;
// using System.Collections.Generic;
// using System.Collections.Immutable;
// using System.Diagnostics.CodeAnalysis;
// using System.Linq;

// using Neo.SmartContract.Native;

// namespace Neo.BlockchainToolkit
// {
//     public static partial class ContractInvocationParser
//     {
//         class ContractBindingVisitor : ContractInvocationVisitor
//         {
//             ImmutableList<ContractInvocation> invocations = ImmutableList<ContractInvocation>.Empty;
//             readonly ICollection<Diagnostic> diagnostics;
//             readonly TryGetContractHash tryGetContractHash;

//             public IReadOnlyList<ContractInvocation> Invocations => invocations;
//             public bool Success => Diagnostic.Success(diagnostics);

//             public ContractBindingVisitor(TryGetContractHash tryGetContractHash, ICollection<Diagnostic> diagnostics)
//             {
//                 this.tryGetContractHash = tryGetContractHash;
//                 this.diagnostics = diagnostics;
//             }

//             public override void Visit(ContractInvocation invocation)
//             {
//                 if (invocation.Contract.TryPickT1(out var contract, out _))
//                 {
//                     if (contract.StartsWith("#"))
//                     {
//                         if (UInt160.TryParse(contract.Substring(1), out var hash))
//                         {
//                             invocation = invocation with { Contract = hash };
//                         }
//                         else
//                         {
//                             diagnostics.Add(Diagnostic.Error($"Could not parse {contract} contract hash"));
//                         }
//                     }
//                     else
//                     {
//                         if (tryGetContractHash(contract, out var hash))
//                         {
//                             invocation = invocation with { Contract = hash };
//                         }
//                         if (NativeContract.Contracts.TryFind(
//                                 nc => nc.Name.Equals(contract, StringComparison.InvariantCultureIgnoreCase), 
//                                 out var result))
//                         {
//                             invocation = invocation with { Contract = result.Hash };
//                         } 
//                         else if (UInt160.TryParse(contract, out hash))
//                         {
//                             invocation = invocation with { Contract = hash };
//                         }
//                         else
//                         {
//                             diagnostics.Add(Diagnostic.Error($"Could not resolve {contract} to a contract"));
//                         }
//                     }
//                 }

//                 invocations = invocations.Add(invocation);
//             }
//         }
//     }
// }


