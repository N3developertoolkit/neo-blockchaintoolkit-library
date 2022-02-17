using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using Neo.SmartContract;
using OneOf;

namespace Neo.BlockchainToolkit.ContractDataDef
{
    using FieldDef = OneOf<ContractParameterType, StructDef>;
    using UnboundFieldDef = OneOf<ContractParameterType, string>;

    internal record UnboundStructDef(string Name, IReadOnlyList<(string name, UnboundFieldDef type)> Fields);

    public readonly struct StructDef : IEquatable<StructDef>
    {
        public readonly string Name = string.Empty;
        public readonly IReadOnlyList<(string name, FieldDef type)> Fields = Array.Empty<(string, FieldDef)>();

        public StructDef(string name, IReadOnlyList<(string name, FieldDef type)> fields)
        {
            Name = name;
            Fields = fields;
        }

        public bool Equals(StructDef other)
        {
            if (Name != other.Name) return false;
            if (Fields.Count != other.Fields.Count) return false;
            for (int i = 0; i < Fields.Count; i++)
            {
                var f = Fields[i];
                var o = other.Fields[i];
                if (f.name != o.name) return false;
                if (f.type.Index != o.type.Index) return false;
                if (f.type.Index == 0)
                {
                    if (f.type.AsT0 != o.type.AsT0) return false;
                }
                else
                {
                    if (!f.type.AsT1.Equals(o.type.AsT1)) return false;
                }
            }

            return true;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is StructDef value && Equals(value);
        }

        public override int GetHashCode()
        {
            HashCode hash = default;
            hash.Add(Name);
            hash.Add(Fields.Count);
            for (int i = 0; i < Fields.Count; i++)
            {
                hash.Add(Fields[i].name);
                hash.Add(Fields[i].type);
            }
            return hash.ToHashCode();
        }

        public static bool operator ==(in StructDef left, in StructDef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in StructDef left, in StructDef right)
        {
            return !left.Equals(right);
        }

        internal static IEnumerable<UnboundStructDef> ParseStructProp(JsonElement json)
        {
            if (json.TryGetProperty("struct", out var structProp))
            {
                if (structProp.ValueKind != JsonValueKind.Object) throw new Exception();
                return structProp.EnumerateObject().Select(ParseStructDef);
            }
            else
            {
                return Enumerable.Empty<UnboundStructDef>();
            }
        }

        internal static UnboundStructDef ParseStructDef(JsonProperty prop)
        {
            if (prop.Value.ValueKind != JsonValueKind.Array) throw new Exception();

            var fields = prop.Value.EnumerateArray().Select(j =>
            {
                var name = j.GetProperty("name").GetString() ?? throw new Exception();
                var typeStr = j.GetProperty("type").GetString() ?? throw new Exception();
                var type = Enum.TryParse<ContractParameterType>(typeStr, true, out var _type)
                    ? UnboundFieldDef.FromT0(_type)
                    : UnboundFieldDef.FromT1(typeStr);

                return (name, type);
            });

            return new UnboundStructDef(prop.Name, fields.ToArray());
        }

        internal static IEnumerable<StructDef> BindStructs(IEnumerable<UnboundStructDef> unboundStructs)
        {
            var unboundStructMap = unboundStructs.ToDictionary(s => s.Name);

            // verify all the StructDef field types are defined
            List<string> unbindableFields = new();
            foreach (var s in unboundStructMap.Values)
            {
                foreach (var f in s.Fields)
                {
                    if (f.type.IsT1 && !unboundStructMap.ContainsKey(f.type.AsT1))
                    {
                        unbindableFields.Add($"{s.Name}.{f.name} ({f.type.AsT1})");
                    }
                }
            }
            if (unbindableFields.Count > 0) throw new Exception(string.Join(',', unbindableFields));

            // loop thru the unbound structs, attempting to bind any unbound fields, until all structs are bound
            var boundStructMap = new Dictionary<string, StructDef>();
            while (boundStructMap.Count < unboundStructMap.Count)
            {
                var boundCount = boundStructMap.Count;
                foreach (var (name, @struct) in unboundStructMap)
                {
                    // if struct is already bound, skip it
                    if (boundStructMap.ContainsKey(name)) continue;

                    // loop thru the fields and attempt to bind each
                    List<(string name, FieldDef type)> fields = new(@struct.Fields.Count);
                    foreach (var f in @struct.Fields)
                    {
                        if (f.type.TryPickT0(out var cpt, out var str))
                        {
                            // if the field type is a ContractParameterType, it's already bound
                            fields.Add((f.name, cpt));
                        }
                        else
                        {
                            // if the field type is a string, check for a matching bound type
                            if (boundStructMap.TryGetValue(str, out var def))
                            {
                                fields.Add((f.name, def));
                            }
                            else
                            {
                                // if there is no matching bound field type, the struct can't be bound at this time so quit iterating
                                break;
                            }
                        }
                    }

                    // if all the fields are bound, bind the struct def
                    if (fields.Count == @struct.Fields.Count)
                    {
                        var structDef = new StructDef(@struct.Name, fields);
                        boundStructMap.Add(name, structDef);
                    }
                }

                // if no progress was made in a given loop, binding is stuck so error out
                if (boundCount >= boundStructMap.Count) throw new Exception();
            }

            return boundStructMap.Values;
        }
    }
}
