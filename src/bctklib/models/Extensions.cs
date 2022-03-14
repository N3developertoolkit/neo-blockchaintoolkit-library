using System;
using System.Collections.Generic;
using System.Linq;
using OneOf;
using NotFound = OneOf.Types.NotFound;

namespace Neo.BlockchainToolkit.Models
{
    static class Extensions
    {
        public static IEnumerable<T> Ensure<T>(this IEnumerable<T>? @this) => @this ?? Enumerable.Empty<T>();
        public static IEnumerable<T> Validate<T>(this IEnumerable<T> @this, Action<IReadOnlyList<T>> validate)
        {
            validate(@this as IReadOnlyList<T> ?? @this.ToArray());
            return @this;
        }
    }
}
