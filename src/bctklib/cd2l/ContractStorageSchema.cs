using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit
{
    public readonly struct ContractStorageSchema
    {
        public readonly IReadOnlyList<StructDef> StructDefs = Array.Empty<StructDef>();
        public readonly IReadOnlyList<StorageDef> StorageDefs = Array.Empty<StorageDef>();

        public ContractStorageSchema(IReadOnlyList<StructDef> structDefs, IReadOnlyList<StorageDef> storageDefs)
        {
            StructDefs = structDefs;
            StorageDefs = storageDefs;
        }

        public static ContractStorageSchema Parse(JObject json)
        {
            var structs = StructDef.ParseStructDefs(json);
            var storages = StorageDef.Parse(json, structs);
            
            return new ContractStorageSchema(structs.ToArray(), storages.ToArray());
        }
    }
}
