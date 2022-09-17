using System;
using System.Buffers;

namespace Neo.BlockchainToolkit.Persistence
{
    class NullMemoryOwner<T> : IMemoryOwner<T>
    {
        public static readonly NullMemoryOwner<T> Instance = new();

        public Memory<T> Memory => default;

        public void Dispose() { }
    }
}