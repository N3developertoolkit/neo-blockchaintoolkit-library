using System;
using System.Collections.Generic;
using System.Linq;
using OneOf;

namespace Neo.BlockchainToolkit.Models
{
    public readonly record struct KeySegment(string Name, PrimitiveType Type);

    public readonly struct StorageGroup : IEquatable<StorageGroup>
    {
        public readonly string Name;
        public readonly ReadOnlyMemory<byte> KeyPrefix;
        public readonly IReadOnlyList<KeySegment> KeySegments;
        public readonly ContractType ValueType;

        public StorageGroup(string name, byte keyPrefix, ContractType valueType)
            : this(name, new[] { keyPrefix }, Array.Empty<KeySegment>(), valueType)
        {
        }

        public StorageGroup(string name, byte keyPrefix, ContractType valueType, params KeySegment[] keySegments)
            : this(name, new[] { keyPrefix }, keySegments, valueType)
        {
        }

        public StorageGroup(string name, ReadOnlyMemory<byte> keyPrefix, IReadOnlyList<KeySegment> keySegments, ContractType valueType)
        {
            Name = name;
            KeyPrefix = keyPrefix;
            KeySegments = keySegments;
            ValueType = valueType;
        }

        public bool Equals(StorageGroup other)
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