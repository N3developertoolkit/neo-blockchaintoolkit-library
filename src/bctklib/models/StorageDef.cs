using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit.Models
{
    public readonly struct StorageDef : IEquatable<StorageDef>
    {
        public readonly record struct KeySegment(string Name, PrimitiveType Type);

        public readonly string Name;
        public readonly ReadOnlyMemory<byte> KeyPrefix;
        public readonly IReadOnlyList<KeySegment> KeySegments;
        public readonly ContractType ValueType;

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

        internal static IEnumerable<StorageDef> Parse(JObject json, IReadOnlyList<StructDef> structs)
        {
            var structMap = structs.ToDictionary(s => s.Name);

            if (json.TryGetValue("storage", out var storageToken))
            {
                if (storageToken.Type != JTokenType.Object) throw new Exception();
                return ((IDictionary<string, JToken?>)json).Select(kvp => ParseStorageDef(kvp, structMap));
            }
            else
            {
                return Enumerable.Empty<StorageDef>();
            }
        }

        internal static StorageDef ParseStorageDef(KeyValuePair<string, JToken?> kvp, IReadOnlyDictionary<string, StructDef> structMap)
        {
            var (name, storageToken) = kvp;
            if (storageToken is null || storageToken.Type != JTokenType.Object) throw new JsonException("invalid storage def JSON");

            var keyToken = storageToken.SelectToken("key");
            var prefix = ParseKeyPrefix(keyToken);
            var segments = ParseKeySegments(keyToken);

            var value = storageToken.Value<string>("value") ?? throw new JsonException("invalid storage value type JSON");
            ContractType valueType = Enum.TryParse<PrimitiveType>(value, out var primitive)
                ? new PrimitiveContractType(primitive)
                : structMap.TryGetValue(value, out var structDef)
                    ? structDef.Type
                    : throw new JsonException($"unrecognized storage value type {value}");

            return new StorageDef(name, prefix, segments, valueType);
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

            throw new JsonException("Invalid key prefix JSON");
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

            throw new JsonException("Invalid Key Segment JSON");
        }

        internal static KeySegment ParseKeySegment(JToken segmentToken)
        {
            if (segmentToken.Type != JTokenType.Object) throw new JsonException("Invalid Key Segment JSON");

            var segmentName = segmentToken.Value<string>("name") ?? throw new JsonException("Invalid Key Segment name JSON");
            var segmentType = Enum.Parse<PrimitiveType>(segmentToken.Value<string>("type")
                ?? throw new JsonException("Invalid Key Segment type JSON"));
            return new KeySegment(segmentName, segmentType);
        }
    }
}