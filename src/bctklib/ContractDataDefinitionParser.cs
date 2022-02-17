using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Neo.SmartContract;
using OneOf;

namespace Neo.BlockchainToolkit
{
    using FieldDef = OneOf<ContractParameterType, StructDef>;
    using UnboundFieldDef = OneOf<ContractParameterType, string>;

    public record StructDef : IEquatable<StructDef>
    {
        public string Name { get; init; } = string.Empty;
        public IReadOnlyList<(string name, FieldDef type)> Fields { get; init; } = Array.Empty<(string, FieldDef)>();

        public override int GetHashCode()
        {
            var hash = default(HashCode);
            hash.Add(this.Name);
            hash.Add(this.Fields.Count);
            for (int i = 0; i < Fields.Count; i++)
            {
                var (name, type) = this.Fields[i];
                hash.Add(name);
                hash.Add(type);
            }
            return hash.ToHashCode();
        }

        public virtual bool Equals(StructDef? other)
        {
            if (object.ReferenceEquals(this, other)) return true;
            if (other is null) return false;

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
    }

    internal record UnboundStructDef
    {
        public string Name = string.Empty;
        public IReadOnlyList<(string name, UnboundFieldDef type)> Fields = Array.Empty<(string, UnboundFieldDef)>();
    }

    public record ContractDataDef
    {
        public IReadOnlyList<StructDef> Structs = Array.Empty<StructDef>();
        // public IReadOnlyList<StorageDef> Storages = Array.Empty<StorageDef>();

        public static ContractDataDef Parse(JsonDocument json)
        {
            var unboundStructs = ParseStructs(json.RootElement);
            var structs = BindStructs(unboundStructs);



            // var storages = json.RootElement.TryGetProperty("storage", out var jsonStorage)
            //     ? jsonStorage.EnumerateObject().Select(StorageDef.Parse).ToArray()
            //     : Array.Empty<StorageDef>();

            return new ContractDataDef
            {
                Structs = structs.ToArray(),
                // Storages = storages,
            };
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

            var boundStructMap = new Dictionary<string, StructDef>();
            int count = 0;
            while (boundStructMap.Count < unboundStructMap.Count)
            {
                // make sure we don't get into an infinite loop
                if (count++ > unboundStructMap.Count) throw new Exception();

                foreach (var (name, @struct) in unboundStructMap)
                {
                    // if we've already processed @struct, skip it
                    if (boundStructMap.ContainsKey(name)) continue;

                    // try to bind the unbound fields 
                    List<(string name, FieldDef type)> fields = new(@struct.Fields.Count);
                    foreach (var f in @struct.Fields)
                    {
                        if (f.type.TryPickT0(out var cpt, out var str))
                        {
                            // if the type is a ContractParameterType, it's already bound
                            fields.Add((f.name, cpt));
                        }
                        else
                        {
                            // if the type is a string, check to see that type is already bound
                            if (boundStructMap.TryGetValue(str, out var def))
                            {
                                fields.Add((f.name, def));
                            }
                            else
                            {
                                // if the type isn't bound already, we can't bind the struct so we can quit iterating
                                break;
                            }
                        }
                    }

                    // if we bound all the fields, bind the struct def
                    if (fields.Count == @struct.Fields.Count)
                    {
                        boundStructMap.Add(name, new StructDef
                        {
                            Name = @struct.Name,
                            Fields = fields
                        });
                    }
                }
            }

            return boundStructMap.Values;
        }

        internal static IEnumerable<UnboundStructDef> ParseStructs(JsonElement json)
        {
            if (json.TryGetProperty("struct", out var structProp))
            {
                if (structProp.ValueKind != JsonValueKind.Object) throw new Exception();
                return structProp.EnumerateObject().Select(ParseStruct);
            }
            else
            {
                return Enumerable.Empty<UnboundStructDef>();
            }
        }

        internal static UnboundStructDef ParseStruct(JsonProperty prop)
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

            return new UnboundStructDef
            {
                Name = prop.Name,
                Fields = fields.ToArray()
            };
        }
    }
}
