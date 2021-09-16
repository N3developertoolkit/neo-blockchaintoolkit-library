using System;
using System.Collections.Generic;
using Neo.Persistence;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    static class Extensions
    {
        public static IEnumerable<(byte[] key, byte[] value)> Seek(this RocksDb db, ColumnFamilyHandle columnFamily, byte[]? key, SeekDirection direction, ReadOptions? readOptions)
        {
            key ??= Array.Empty<byte>();
            using var iterator = db.NewIterator(columnFamily, readOptions);

            Func<Iterator> iteratorNext;
            if (direction == SeekDirection.Forward)
            {
                iterator.Seek(key);
                iteratorNext = iterator.Next;
            }
            else
            {
                iterator.SeekForPrev(key);
                iteratorNext = iterator.Prev;
            }

            while (iterator.Valid())
            {
                yield return (iterator.Key(), iterator.Value());
                iteratorNext();
            }
        }
    }
}