using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit.Models
{
    public readonly record struct ContractStorageSchema
    {
        public readonly IReadOnlyList<StructContractType> StructDefs;
        public readonly IReadOnlyList<StorageDef> StorageDefs;

        public ContractStorageSchema()
        {
            StructDefs = Array.Empty<StructContractType>();
            StorageDefs = Array.Empty<StorageDef>();
        }

        public ContractStorageSchema(IReadOnlyList<StructContractType> structDefs, IReadOnlyList<StorageDef> storageDefs)
        {
            StructDefs = structDefs;
            StorageDefs = storageDefs;
        }

        public static ContractStorageSchema Parse(Neo.IO.Json.JObject json)
        {
            return Parse(JObject.Parse(json.ToString()));
        }

        public static ContractStorageSchema Parse(JToken json)
        {
            if (json is JObject jObject)
            {
                var structs = ParseStructProperty(jObject).ToArray();
                var storages = ParseStorageProperty(jObject, structs).ToArray();
                
                return new ContractStorageSchema(structs, storages);
            }
            else
            {
                throw new JsonException($"Invalid schema json type {json.Type}");
            }
        }

        internal static IEnumerable<StructContractType> ParseStructProperty(JObject json)
        {
            if (json.TryGetValue("struct", out var structToken))
            {
                if (structToken is JObject structJson)
                {
                    var unboundStructs = ((IDictionary<string, JToken?>)structJson).Select(ParseStructDef);
                    return BindStructDefs(unboundStructs);
                }
                else
                {
                    throw new JsonException($"Invalid struct JSON type {structToken.Type}");
                }
            }
            else
            {
                return Enumerable.Empty<StructContractType>();
            }
        }

        internal static IEnumerable<StorageDef> ParseStorageProperty(JObject json, IReadOnlyList<StructContractType> structs)
        {
            var structMap = structs.ToDictionary(s => s.Name);

            if (json.TryGetValue("storage", out var storageToken))
            {
                if (storageToken is JObject storageJson)
                {
                    return ((IDictionary<string, JToken?>)storageJson).Select(kvp => ParseStorageDef(kvp, structMap));
                }
                else
                {
                    throw new JsonException($"Invalid storage JSON type {storageToken.Type}");
                }
            }
            else
            {
                return Enumerable.Empty<StorageDef>();
            }
        }

        internal static (string name, IReadOnlyList<(string name, string type)> fields) ParseStructDef(KeyValuePair<string, JToken?> kvp)
        {
            var (name, fieldsToken) = kvp;
            if (fieldsToken is null) throw new JsonException("Null struct def type");

            if (fieldsToken is JArray fieldsArray)
            {
                List<(string name, string type)> fields = new(fieldsArray.Count);
                for (int i = 0; i < fieldsArray.Count; i++)
                {
                    var fieldName = fieldsArray[i].Value<string>("name") ?? throw new JsonException("Missing name property");
                    var fieldType = fieldsArray[i].Value<string>("type") ?? throw new JsonException("Missing type property");
                    fields.Add((fieldName, fieldType));
                }
                return (name, fields);
            }
            else
            {
                throw new JsonException($"Invalid struct JSON type {fieldsToken.Type}");
            }
        }

        internal static IEnumerable<StructContractType> BindStructDefs(IEnumerable<(string name, IReadOnlyList<(string name, string type)> fields)> unboundStructs)
        {
            var unboundStructMap = unboundStructs.ToDictionary(s => s.name);

            // loop thru the unbound structs, attempting to bind any unbound fields, until all structs are bound
            var boundStructMap = new Dictionary<string, StructContractType>();
            while (boundStructMap.Count < unboundStructMap.Count)
            {
                var boundCount = boundStructMap.Count;
                foreach (var (structName, @struct) in unboundStructMap)
                {
                    // if struct is already bound, skip it
                    if (boundStructMap.ContainsKey(structName)) continue;

                    List<(string name, ContractType type)> fields = new(@struct.fields.Count);
                    foreach (var (fieldName, fieldType) in @struct.fields)
                    {
                        if (Enum.TryParse<PrimitiveType>(fieldType, out var primitive))
                        {
                            fields.Add((fieldName, new PrimitiveContractType(primitive)));
                        }
                        else if (boundStructMap.TryGetValue(fieldType, out var structDef))
                        {
                            fields.Add((fieldName, structDef));
                        }
                        else
                        {
                            break;
                        }
                    }
                    
                    // if all the fields are bound, bind the struct def
                    if (fields.Count == @struct.fields.Count)
                    {
                        boundStructMap.Add(structName, new StructContractType(structName, fields));
                    }
                }

                // if no progress was made in a given loop, binding is stuck so error out
                if (boundCount >= boundStructMap.Count)
                {
                    var unboundStructNames = unboundStructMap.Keys.Where(k => !boundStructMap.ContainsKey(k));
                    throw new Exception($"Failed to bind {string.Join(",", unboundStructNames)} structs");
                }
            }

            return boundStructMap.Values;
        }

        internal static StorageDef ParseStorageDef(KeyValuePair<string, JToken?> kvp, IReadOnlyDictionary<string, StructContractType> structMap)
        {
            var (name, storageToken) = kvp;
            if (storageToken is null) throw new JsonException("Null storage def type");
            if (storageToken.Type != JTokenType.Object) throw new JsonException($"invalid storage def JSON type {storageToken.Type}");

            var keyToken = storageToken.SelectToken("key");
            var prefix = ParseKeyPrefix(keyToken);
            var segments = ParseKeySegments(keyToken);

            var value = storageToken.Value<string>("value") ?? throw new JsonException("invalid storage value type JSON");
            ContractType valueType = Enum.TryParse<PrimitiveType>(value, out var primitive)
                ? new PrimitiveContractType(primitive)
                : structMap.TryGetValue(value, out var structDef)
                    ? structDef
                    : throw new JsonException($"unrecognized storage value type {value}");

            return new StorageDef(name, prefix, segments, valueType);
        }

        internal static ReadOnlyMemory<byte> ParseKeyPrefix(JToken? keyToken)
        {
            if (keyToken is null) throw new JsonException("null key token");
            var prefixToken = keyToken.SelectToken("prefix") ?? throw new JsonException("missing prefix token");

            if (prefixToken.Type == JTokenType.String)
            {
                var prefixString = prefixToken.Value<string>() ?? throw new JsonException("missing prefix value");
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

            throw new JsonException($"Invalid key prefix JSON type {prefixToken.Type}");
        }

        internal static IReadOnlyList<StorageDef.KeySegment> ParseKeySegments(JToken? keyToken)
        {
            var segmentsToken = keyToken?.SelectToken("segments");
            if (segmentsToken is null) return Array.Empty<StorageDef.KeySegment>();

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

        internal static StorageDef.KeySegment ParseKeySegment(JToken segmentToken)
        {
            if (segmentToken.Type != JTokenType.Object) throw new JsonException($"Invalid Key Segment JSON type {segmentToken.Type}");

            var segmentName = segmentToken.Value<string>("name") ?? throw new JsonException("missing Key Segment name");
            var segmentType = Enum.Parse<PrimitiveType>(segmentToken.Value<string>("type")
                ?? throw new JsonException("Invalid Key Segment type JSON"));

            return new StorageDef.KeySegment(segmentName, segmentType);
        }
    }
}