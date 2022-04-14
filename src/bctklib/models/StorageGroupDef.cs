using System;
using System.Collections.Generic;
using System.Linq;
using OneOf;

namespace Neo.BlockchainToolkit.Models
{
    // native contracts can use fixed-width big endian key segments
    public readonly record struct BigEndianUInt32;
    public readonly record struct BigEndianUInt64;

    public readonly record struct KeySegment(string Name, OneOf<PrimitiveType, BigEndianUInt32, BigEndianUInt64> Type);

    public readonly struct StorageGroupDef : IEquatable<StorageGroupDef>
    {
        public readonly string Name;
        public readonly ReadOnlyMemory<byte> KeyPrefix;
        public readonly IReadOnlyList<KeySegment> KeySegments;
        public readonly ContractType ValueType;

        public StorageGroupDef(string name, byte keyPrefix, ContractType valueType)
            : this(name, new[] { keyPrefix }, Array.Empty<KeySegment>(), valueType)
        {
        }

        public StorageGroupDef(string name, byte keyPrefix, ContractType valueType, params KeySegment[] keySegments)
            : this(name, new[] { keyPrefix }, keySegments, valueType)
        {
        }

        public StorageGroupDef(string name, ReadOnlyMemory<byte> keyPrefix, IReadOnlyList<KeySegment> keySegments, ContractType valueType)
        {
            Name = name;
            KeyPrefix = keyPrefix;
            KeySegments = keySegments;
            ValueType = valueType;
        }

        public bool Equals(StorageGroupDef other)
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