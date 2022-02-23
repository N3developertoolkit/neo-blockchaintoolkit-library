using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit.Models
{
    public readonly struct StructDef : IEquatable<StructDef>
    {
        public readonly string Name;
        public readonly StructContractType Type;

        public StructDef(string name, StructContractType type)
        {
            Name = name;
            Type = type;
        }

        public bool Equals(StructDef other)
        {
            return Name != other.Name && Type.Equals(other.Type);
        }

        internal static IEnumerable<StructDef> Parse(JObject json)
        {
            if (json.TryGetValue("struct", out var structToken))
            {
                if (structToken.Type != JTokenType.Object) throw new Exception();
                return ParseStructDefs((JObject)structToken);
            }
            else
            {
                return Enumerable.Empty<StructDef>();
            }
        }

        internal static IEnumerable<StructDef> ParseStructDefs(JObject json)
        {
            var unboundStructs = ((IDictionary<string, JToken?>)json).Select(ParseStructDef);
            return BindStructDefs(unboundStructs);
        }

        internal static (string name, IReadOnlyList<(string name, string type)> fields) ParseStructDef(KeyValuePair<string, JToken?> kvp)
        {
            var (name, fieldsToken) = kvp;
            if (fieldsToken is null || fieldsToken.Type != JTokenType.Array) throw new Exception();
            var fieldsArray = (JArray)fieldsToken;

            List<(string name, string type)> fields = new(fieldsArray.Count);
            for (int i = 0; i < fieldsArray.Count; i++)
            {
                var fieldName = fieldsArray[i].Value<string>("name") ?? throw new Exception();
                var fieldType = fieldsArray[i].Value<string>("type") ?? throw new Exception();
                fields.Add((fieldName, fieldType));
            }
            return (name, fields);
        }

        internal static IEnumerable<StructDef> BindStructDefs(IEnumerable<(string name, IReadOnlyList<(string name, string type)> fields)> unboundStructs)
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
                        boundStructMap.Add(structName, new StructContractType(fields));
                    }
                }

                // if no progress was made in a given loop, binding is stuck so error out
                if (boundCount >= boundStructMap.Count)
                {
                    var unboundStructNames = unboundStructMap.Keys.Where(k => !boundStructMap.ContainsKey(k));
                    throw new Exception($"Failed to bind {string.Join(",", unboundStructNames)}");
                }
            }

            return boundStructMap.Select(kvp => new StructDef(kvp.Key, kvp.Value));
        }
    }
}