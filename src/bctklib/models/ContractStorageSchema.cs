using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit.Models
{
    public readonly record struct ContractStorageSchema
    {
        public readonly IReadOnlyList<StructDef> StructDefs;
        public readonly IReadOnlyList<StorageDef> StorageDefs;

        public ContractStorageSchema(IReadOnlyList<StructDef> structDefs, IReadOnlyList<StorageDef> storageDefs)
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
            return json.Type == JTokenType.Object ? Parse((JObject)json) : throw new Exception();
        }

        public static ContractStorageSchema Parse(JObject json)
        {
            var structs = StructDef.Parse(json).ToArray();
            var storages = StorageDef.Parse(json, structs).ToArray();
            
            return new ContractStorageSchema(structs, storages);
        }
    }
}