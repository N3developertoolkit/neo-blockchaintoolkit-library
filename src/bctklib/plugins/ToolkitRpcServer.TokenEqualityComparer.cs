using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.BlockchainToolkit.Plugins
{
    public static partial class ToolkitRpcServer
    {
        class TokenEqualityComparer : IEqualityComparer<(UInt160 scriptHash, ReadOnlyMemory<byte> tokenId)>
        {
            public static TokenEqualityComparer Instance = new();

            private TokenEqualityComparer() { }

            public bool Equals((UInt160 scriptHash, ReadOnlyMemory<byte> tokenId) x, (UInt160 scriptHash, ReadOnlyMemory<byte> tokenId) y)
                => x.scriptHash.Equals(y.scriptHash)
                    && x.tokenId.Span.SequenceEqual(y.tokenId.Span);

            public int GetHashCode((UInt160 scriptHash, ReadOnlyMemory<byte> tokenId) obj)
            {
                HashCode code = new();
                code.Add(obj.scriptHash);
                code.AddBytes(obj.tokenId.Span);
                return code.ToHashCode();
            }
        }
    }
}
