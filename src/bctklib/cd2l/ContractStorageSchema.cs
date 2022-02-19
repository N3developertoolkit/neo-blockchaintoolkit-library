using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit
{
    public record ContractStorageSchema
    {
        public IReadOnlyList<StructDef> StructDefs { get; init; } = Array.Empty<StructDef>();
        public IReadOnlyList<StorageDef> StorageDefs { get; init; } = Array.Empty<StorageDef>();

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
            
            return new ContractStorageSchema
            {
                StructDefs = structs,
                StorageDefs = storages,
            };
        }
    }
}
