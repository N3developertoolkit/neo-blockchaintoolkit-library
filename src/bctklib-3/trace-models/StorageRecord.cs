using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Neo.Ledger;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public class StorageRecord : ITraceDebugRecord
    {
        public const int RecordKey = 6;

        [Key(0)]
        public readonly UInt160 ScriptHash;

        [Key(1)]
        public readonly IReadOnlyDictionary<byte[], StorageItem> Storages;

        public StorageRecord(UInt160 scriptHash, IReadOnlyDictionary<byte[], StorageItem> storages)
        {
            ScriptHash = scriptHash;
            Storages = storages;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, UInt160 scriptHash, IEnumerable<(StorageKey key, StorageItem item)> storages)
        {
            var count = storages.Count();
            if (count > 0)
            {
                var byteArrayResolver = options.Resolver.GetFormatterWithVerify<byte[]>();
                var storageItemResolver = options.Resolver.GetFormatterWithVerify<StorageItem>();

                var mpWriter = new MessagePackWriter(writer);
                mpWriter.WriteArrayHeader(2);
                mpWriter.WriteInt32(RecordKey);
                mpWriter.WriteArrayHeader(2);
                options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref mpWriter, scriptHash, options);
                mpWriter.WriteMapHeader(count);
                foreach (var (key, item) in storages)
                {
                    byteArrayResolver.Serialize(ref mpWriter, key.Key, options);
                    storageItemResolver.Serialize(ref mpWriter, item, options);
                }
                mpWriter.Flush();
            }
        }
    }
}
