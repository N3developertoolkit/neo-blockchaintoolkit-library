using System;
using System.Collections.Generic;
using System.Linq;
using Neo.SmartContract;
using Newtonsoft.Json.Linq;
using OneOf;

namespace Neo.BlockchainToolkit
{
    using StorageValueTypeDef = OneOf<ContractParameterType, StructDef>;

    public readonly struct StorageDef : IEquatable<StorageDef>
    {
        public readonly record struct KeySegment(string Name, ContractParameterType Type);

        public readonly string Name = string.Empty;
        public readonly ReadOnlyMemory<byte> KeyPrefix;
        public readonly IReadOnlyList<KeySegment> KeySegments = Array.Empty<KeySegment>();
        public readonly StorageValueTypeDef Value;

        public StorageDef(string name, ReadOnlyMemory<byte> keyPrefix, IReadOnlyList<KeySegment> keySegments, StorageValueTypeDef value)
        {
            Name = name;
            KeyPrefix = keyPrefix;
            KeySegments = keySegments;
            Value = value;
        }

        public bool Equals(StorageDef other)
        {
            if (Name != other.Name) return false;
            if (!Value.Equals(other.Value)) return false;
            if (!KeyPrefix.Span.SequenceEqual(other.KeyPrefix.Span)) return false;
            if (KeySegments.Count != other.KeySegments.Count) return false;
            for (int i = 0; i < KeySegments.Count; i++)
            {
                if (!KeySegments[i].Equals(other.KeySegments[i])) return false;
            }
            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is StorageDef value && Equals(value);
        }

        public override int GetHashCode()
        {
            HashCode hash = default;
            hash.Add(Name);
            hash.Add(Value);
            hash.Add(KeyPrefix);
            for (int i = 0; i < KeySegments.Count; i++)
            {
                hash.Add(KeySegments[i]);
            }
            return hash.ToHashCode();
        }

        internal static IEnumerable<StorageDef> Parse(JObject json, IReadOnlyList<StructDef> structs)
            => Parse(json, structs.ToDictionary(s => s.Name));

        internal static IEnumerable<StorageDef> Parse(JObject json, IReadOnlyDictionary<string, StructDef> structMap)
        {
            if (json.TryGetValue("storage", out var storageToken))
            {
                if (storageToken.Type != JTokenType.Object) throw new Exception();
                return ParseStorageDefs((JObject)storageToken, structMap);
            }
            else
            {
                return Enumerable.Empty<StorageDef>();
            }
        }

        internal static IEnumerable<StorageDef> ParseStorageDefs(JObject json, IReadOnlyDictionary<string, StructDef> structMap)
        {
            return ((IDictionary<string, JToken?>)json).Select(kvp => ParseStorageDef(kvp, structMap));
        }

        internal static StorageDef ParseStorageDef(KeyValuePair<string, JToken?> kvp, IReadOnlyDictionary<string, StructDef> structMap)
        {
            var (name, storageToken) = kvp;
            if (storageToken is null || storageToken.Type != JTokenType.Object) throw new Exception();

            var keyToken = storageToken.SelectToken("key");
            var prefix = ParseKeyPrefix(keyToken);
            var segments = ParseKeySegments(keyToken);

            var valueStr = storageToken.Value<string>("value") ?? throw new Exception();
            var value = Enum.TryParse<ContractParameterType>(valueStr, out var _value)
                ? StorageValueTypeDef.FromT0(_value)
                : structMap.TryGetValue(valueStr, out var value_)
                    ? StorageValueTypeDef.FromT1(value_)
                    : throw new Exception();


            return new StorageDef(name, prefix, segments, value);
        }

        internal static ReadOnlyMemory<byte> ParseKeyPrefix(JToken? keyToken)
        {
            var prefixToken = keyToken?.SelectToken("prefix") ?? throw new Exception();
            if (prefixToken.Type == JTokenType.String)
            {
                var prefixString = prefixToken.Value<string>() ?? throw new Exception();
                return Neo.Utility.StrictUTF8.GetBytes(prefixString);
            }

            if (prefixToken.Type == JTokenType.Integer)
            { 
                return new[] { (byte)prefixToken };
            }

            if (prefixToken.Type == JTokenType.Array)
            {
                return prefixToken.Select(j => (byte)j).ToArray();
            }

            throw new Exception();
        }

        internal static KeySegment ParseKeySegment(JToken segmentToken)
        {
            if (segmentToken.Type != JTokenType.Object) throw new Exception();

            var segmentName = segmentToken.Value<string>("name") ?? throw new Exception();
            var segmentType = Enum.Parse<ContractParameterType>(segmentToken.Value<string>("type") ?? throw new Exception());
            return new KeySegment(segmentName, segmentType);
        }

        internal static IReadOnlyList<KeySegment> ParseKeySegments(JToken? keyToken)
        {
            var segmentsToken = keyToken?.SelectToken("segments");
            if (segmentsToken is null) return Array.Empty<KeySegment>();

            if (segmentsToken.Type == JTokenType.Array)
            {
                return segmentsToken.Select(ParseKeySegment).ToArray();
            }

            if (segmentsToken.Type == JTokenType.Object)
            {
                return new[] { ParseKeySegment(segmentsToken) };
            }

            throw new Exception();
        }
    }
}
