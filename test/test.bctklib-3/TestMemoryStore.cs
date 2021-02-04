using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using OneOf;

namespace test.bctklib3
{
    using TrackingMap = ImmutableSortedDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    class TestMemoryStore : IExpressStore
    {
        readonly static TrackingMap EMPTY_TRACKING_MAP = TrackingMap.Empty.WithComparers(ByteArrayComparer.Default);

        ImmutableDictionary<byte, TrackingMap> trackingMaps = ImmutableDictionary<byte, TrackingMap>.Empty;

        public byte[]? TryGet(byte table, byte[]? key)
        {
            var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
            if (trackingMap.TryGetValue(key ?? Array.Empty<byte>(), out var mapValue))
            {
                return mapValue.Match<byte[]?>(v => v, n => null);
            }

            return null;
        }

        public bool Contains(byte table, byte[]? key) => TryGet(table, key) != null;

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[]? prefix, SeekDirection direction)
        {
            prefix ??= Array.Empty<byte>();
            var comparer = direction == SeekDirection.Forward ? ByteArrayComparer.Default : ByteArrayComparer.Reverse;
            var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;

            return trackingMap
                .Where(kvp => kvp.Value.IsT0)
                .Where(kvp => prefix.Length == 0 || comparer.Compare(kvp.Key, prefix) >= 0)
                .Select(kvp => (kvp.Key, Value: kvp.Value.AsT0))
                .OrderBy(kvp => kvp.Key, comparer);
        }

        public void Put(byte table, byte[]? key, byte[] value)
        {
            var trackingMap = trackingMaps.TryGetValue(table, out var map) ? map : EMPTY_TRACKING_MAP;
            trackingMap = trackingMap.SetItem(key ?? Array.Empty<byte>(), value);
            trackingMaps = trackingMaps.SetItem(table, trackingMap);
        }

        public void PutSync(byte table, byte[]? key, byte[] value)
        {
            throw new NotImplementedException();
        }

        public void Delete(byte table, byte[]? key)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        void IStore.Delete(byte[] key) => Delete(default, key);

        ISnapshot IStore.GetSnapshot()
        {
            throw new NotImplementedException();
        }

        void IStore.Put(byte[] key, byte[] value) => Put(default, key, value);

        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte[] key, SeekDirection direction) => Seek(default, key, direction);

        byte[] IReadOnlyStore.TryGet(byte[] key) => TryGet(default, key)!;

        bool IReadOnlyStore.Contains(byte[] key) => Contains(default, key);
    }
}
