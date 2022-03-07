using System;
using System.Collections.Generic;
using System.Linq;
using OneOf;

namespace Neo.BlockchainToolkit.Models
{
    // Several native contracts use the big-endian encoded block index as a key segment.
    // This is *NOT SUPPORTED* for deployed contracts. There is no string serialized 
    // format for BlockIndex, so it should never appear in the debug info of a deployed
    // contract. 

    public struct BlockIndex { }
    public readonly record struct KeySegment(string Name, OneOf<PrimitiveType, BlockIndex> Type);

    public readonly struct StorageDef : IEquatable<StorageDef>
    {
        public readonly string Name;
        public readonly ReadOnlyMemory<byte> KeyPrefix;
        public readonly IReadOnlyList<KeySegment> KeySegments;
        public readonly ContractType ValueType;

        public StorageDef(string name, byte keyPrefix, ContractType valueType)
            : this(name, new[] { keyPrefix }, Array.Empty<KeySegment>(), valueType)
        {
        }

        public StorageDef(string name, byte keyPrefix, ContractType valueType, params KeySegment[] keySegments)
            : this(name, new[] { keyPrefix }, keySegments, valueType)
        {
        }

        public StorageDef(string name, ReadOnlyMemory<byte> keyPrefix, IReadOnlyList<KeySegment> keySegments, ContractType valueType)
        {
            Name = name;
            KeyPrefix = keyPrefix;
            KeySegments = keySegments;
            ValueType = valueType;
        }

        public bool Equals(StorageDef other)
        {
            if (Name != other.Name) return false;
            if (!KeyPrefix.Span.SequenceEqual(other.KeyPrefix.Span)) return false;
            if (!ValueType.Equals(other.ValueType)) return false;
            if (KeySegments.Count != other.KeySegments.Count) return false;
            for (var i = 0; i < KeySegments.Count; i++)
            {
                if (!KeySegments[i].Equals(other.KeySegments[i])) return false;
            }
            return true;
        }
    }
}