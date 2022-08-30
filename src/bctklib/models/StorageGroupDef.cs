using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using OneOf;

namespace Neo.BlockchainToolkit.Models
{
    public readonly record struct KeySegment(string Name, PrimitiveType Type);

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

        public override bool Equals(object? obj)
        {
            return obj is StorageGroupDef sgd ? Equals(sgd) : false;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Name);
            hash.Add(ValueType);
            hash.Add(KeyPrefix.Span.Length);
            for (int i = 0; i < KeyPrefix.Span.Length; i++)
            {
                hash.Add(KeyPrefix.Span[i]);
            }
            hash.Add(KeySegments.Count);
            for (int i = 0; i < KeySegments.Count; i++)
            {
                hash.Add(KeySegments[i]);
            }
            return hash.ToHashCode();
        }

        public static bool operator ==(StorageGroupDef left, StorageGroupDef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StorageGroupDef left, StorageGroupDef right)
        {
            return !(left == right);
        }
    }
}