using System;
using System.Collections.Generic;
using System.Linq;
using Neo.SmartContract;
using Newtonsoft.Json.Linq;
using OneOf;

namespace Neo.BlockchainToolkit
{
    using StorageValueTypeDef = OneOf<ContractParameterType, StructDef>;
    using KeySegment = OneOf<byte, (string name, ContractParameterType type)>;

    public readonly struct StorageDef : IEquatable<StorageDef>
    {
        public readonly string Name = string.Empty;
        public readonly IReadOnlyList<KeySegment> Key = Array.Empty<KeySegment>();
        public readonly StorageValueTypeDef Value;

        public StorageDef(string name, IReadOnlyList<OneOf<byte, (string name, ContractParameterType type)>> key, StorageValueTypeDef value)
        {
            Name = name;
            Key = key;
            Value = value;
        }

        public bool Equals(StorageDef other)
        {
            if (Name != other.Name) return false;
            if (!Value.Equals(other.Value)) return false;
            if (Key.Count != other.Key.Count) return false;
            for (int i = 0; i < Key.Count; i++)
            {
               if (!Key[i].Equals(other.Key[i])) return false;
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
            hash.Add(Key.Count);
            for (int i = 0; i < Key.Count; i++)
            {
                hash.Add(Key[i]);
            }
            return hash.ToHashCode();
        }

        internal static IEnumerable<StorageDef> Parse(JObject json, IEnumerable<StructDef> structs)
        {
            return Parse(json, structs.ToDictionary(s => s.Name));
        }

        internal static IEnumerable<StorageDef> Parse(JObject json, IDictionary<string, StructDef> structMap)
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

        internal static IEnumerable<StorageDef> ParseStorageDefs(JObject json, IDictionary<string, StructDef> structMap)
        {
            return ((IDictionary<string, JToken?>)json).Select(kvp => ParseStorageDef(kvp, structMap));
        }


        internal static StorageDef ParseStorageDef(KeyValuePair<string, JToken?> kvp, IDictionary<string, StructDef> structMap)
        {
            var (name, storageToken) = kvp;
            if (storageToken is null || storageToken.Type != JTokenType.Object) throw new Exception();

            var valueStr = storageToken.Value<string>("value") ?? throw new Exception();
            var value = Enum.TryParse<ContractParameterType>(valueStr, out var _value)
                ? StorageValueTypeDef.FromT0(_value)
                : structMap.TryGetValue(valueStr, out var value_)
                    ? StorageValueTypeDef.FromT1(value_)
                    : throw new Exception();

            var keyToken = storageToken["key"];
            if (keyToken is null || keyToken.Type != JTokenType.Array) throw new Exception();

            var keyArray = (JArray)keyToken;
            List<KeySegment> keySegments = new(keyArray.Count);
            for (int i = 0; i < keyArray.Count; i++)
            {
                var keySegment = keyArray[i];
                if (keySegment is null) throw new Exception();

                if (keySegment.Type == JTokenType.Integer)
                {
                    keySegments.Add(keySegment.Value<byte>());
                }
                else if (keySegment.Type == JTokenType.Object)
                {
                    var segmentName = keySegment.Value<string>("name") ?? throw new Exception();
                    var segmentTypeStr = keySegment.Value<string>("type") ?? throw new Exception();
                    var segmentType = Enum.Parse<ContractParameterType>(segmentTypeStr);

                    var isValid = segmentType switch {
                        ContractParameterType.Boolean => true,
                        ContractParameterType.Hash160 => true,
                        ContractParameterType.Hash256 => true,
                        _ => false,
                    };
                    if (!isValid) throw new Exception();

                    keySegments.Add((segmentName, segmentType));
                }
                else throw new Exception();
            }

            return new StorageDef(name, keySegments, value);
        }
    }
}
