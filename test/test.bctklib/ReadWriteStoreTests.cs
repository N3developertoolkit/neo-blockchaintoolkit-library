using System;
using System.Linq;
using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using Xunit;

namespace test.bctklib;

using static Utility;

public class ReadWriteStoreTests : IDisposable
{
    // include memory and NeoRocksDb for comparison
    // Note, MemoryStore fails multiple tests. Tracked by https://github.com/neo-project/neo/issues/2758
    public enum StoreType { Memory, MemoryTracking, NeoRocksDb, PersistentTracking, RocksDb }

    readonly CleanupPath path = new CleanupPath();

    public void Dispose()
    {
        path.Dispose();
    }

    [Theory, CombinatorialData]
    public void put_new_value(StoreType storeType)
    {
        using var store = GetStore(storeType);
        var key = Bytes(0);
        var value = Bytes("test-value");
        store.TryGet(key).Should().BeNull();
        store.Put(key, value);
        store.TryGet(key).Should().BeEquivalentTo(value);
    }

    [SkippableTheory, CombinatorialData]
    public void put_overwrite_existing_value(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        SkipNonTrackCombinatorial(storeType, index);
        using var store = GetStore(storeType);
        var (key, value) = TestData.ElementAt(index);
        var newValue = Bytes("test-value");
        store.TryGet(key).Should().BeEquivalentTo(value);
        store.Put(key, newValue);
        store.TryGet(key).Should().BeEquivalentTo(newValue);
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_add(StoreType storeType)
    {
        using var store = GetStore(storeType);
        var key = Bytes(0);
        var value = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, value);

        store.TryGet(key).Should().BeNull();
        snapshot.Commit();
        store.TryGet(key).Should().BeEquivalentTo(value);
    }

    [SkippableTheory, CombinatorialData]
    public void snapshot_commit_update(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        SkipNonTrackCombinatorial(storeType, index);
        using var store = GetStore(storeType);
        var (key, value) = TestData.ElementAt(index);
        var newValue = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, newValue);

        store.TryGet(key).Should().BeEquivalentTo(value);
        snapshot.Commit();
        store.TryGet(key).Should().BeEquivalentTo(newValue);
    }

    [SkippableTheory, CombinatorialData]
    public void snapshot_commit_delete(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        SkipNonTrackCombinatorial(storeType, index);
        using var store = GetStore(storeType);
        var (key, value) = TestData.ElementAt(index);
        using var snapshot = store.GetSnapshot();
        snapshot.Delete(key);

        store.TryGet(key).Should().BeEquivalentTo(value);
        snapshot.Commit();
        store.TryGet(key).Should().BeNull();
    }

    [SkippableTheory, CombinatorialData]
    public void snapshot_isolation_addition(StoreType storeType)
    {
        Skip.If(storeType == StoreType.Memory, "https://github.com/neo-project/neo/issues/2758");
        using var store = GetStore(storeType);
        using var snapshot = store.GetSnapshot();
        var key = Bytes(0);
        var newValue = Bytes("test-value");
        store.Put(key, newValue);
        snapshot.Contains(key).Should().BeFalse();
        snapshot.TryGet(key).Should().BeNull();
    }

    [SkippableTheory, CombinatorialData]
    public void snapshot_isolation_update(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        SkipNonTrackCombinatorial(storeType, index);
        using var store = GetStore(storeType);
        using var snapshot = store.GetSnapshot();
        var (key, value) = TestData.ElementAt(index);
        var newValue = Bytes("test-value");
        store.Put(key, newValue);
        snapshot.Contains(key).Should().BeTrue();
        snapshot.TryGet(key).Should().BeEquivalentTo(value);
    }

    [SkippableTheory, CombinatorialData]
    public void snapshot_isolation_delete(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        Skip.If(storeType == StoreType.Memory, "https://github.com/neo-project/neo/issues/2758");
        SkipNonTrackCombinatorial(storeType, index);
        using var store = GetStore(storeType);
        using var snapshot = store.GetSnapshot();
        var (key, value) = TestData.ElementAt(index);
        store.Delete(key);
        snapshot.Contains(key).Should().BeTrue();
        snapshot.TryGet(key).Should().BeEquivalentTo(value);
    }

    [SkippableTheory, CombinatorialData]
    public void key_instance_isolation(StoreType storeType)
    {
        Skip.If(storeType == StoreType.Memory, "https://github.com/neo-project/neo/issues/2758");
        using var store = GetStore(storeType);
        var key = Bytes(0);
        var value = Bytes("test-value");
        store.Put(key, value);

        key[0] = 0xff;
        store.TryGet(Bytes(0)).Should().BeEquivalentTo(value);
        store.TryGet(key).Should().BeNull();
    }

    [SkippableTheory, CombinatorialData]
    public void value_instance_isolation(StoreType storeType)
    {
        Skip.If(storeType == StoreType.Memory, "https://github.com/neo-project/neo/issues/2758");
        using var store = GetStore(storeType);
        var key = Bytes(0);
        var value = Bytes("test-value");
        store.Put(key, value);

        value[0] = 0xff;
        store.TryGet(key).Should().BeEquivalentTo(Bytes("test-value"));
    }

    [SkippableTheory, CombinatorialData]
    public void put_null_value_throws(StoreType storeType)
    {
        Skip.If(storeType == StoreType.Memory, "https://github.com/neo-project/neo/issues/2758");
        using var store = GetStore(storeType);
        var key = Bytes(0);
        Assert.Throws<NullReferenceException>(() => store.Put(key, null));
    }

    [SkippableTheory, CombinatorialData]
    public void snapshot_put_null_value_throws(StoreType storeType)
    {
        Skip.If(storeType == StoreType.Memory, "https://github.com/neo-project/neo/issues/2758");
        using var store = GetStore(storeType);
        using var snapshot = store.GetSnapshot();
        var key = Bytes(0);
        Assert.Throws<NullReferenceException>(() => snapshot.Put(key, null));
    }

    [Theory, CombinatorialData]
    public void delete_missing_value_no_effect(StoreType storeType)
    {
        using var store = GetStore(storeType);
        var key = Bytes(0);
        store.TryGet(key).Should().BeNull();
        store.Delete(key);
        store.TryGet(key).Should().BeNull();
    }

    static void SkipNonTrackCombinatorial(StoreType storeType, int index)
        => Skip.If(!(storeType == StoreType.MemoryTracking || storeType == StoreType.PersistentTracking)
            && index > 0);

    IStore GetStore(StoreType type) => type switch
    {
        StoreType.Memory => ReadOnlyStoreTests.GetPopulatedMemoryStore(),
        StoreType.RocksDb => GetPopulatedRocksDbStore(path),
        StoreType.NeoRocksDb => GetPopulatedNeoRocksDbStore(path),
        _ => GetPopulatedTrackingStore(type),
    };

    public static RocksDbStore GetPopulatedRocksDbStore(string path)
    {
        using (var db = RocksDbUtility.OpenDb(path))
        {
            RocksDbFixture.Populate(db);
        }

        return new RocksDbStore(RocksDbUtility.OpenDb(path));
    }

    public static IStore GetPopulatedNeoRocksDbStore(string path)
    {
        using (var db = RocksDbUtility.OpenDb(path))
        {
            RocksDbFixture.Populate(db);
        }

        var storeType = typeof(Neo.Plugins.Storage.RocksDBStore).Assembly
            .GetType("Neo.Plugins.Storage.Store");
        var storeCtor = storeType?.GetConstructor(new [] { typeof(string) });
        var store = storeCtor?.Invoke(new object[] { (string)path }) as IStore;
        Skip.If(store is null);
        return store!;
    }

    internal ITrackingStore GetPopulatedTrackingStore(StoreType type)
    {
        ITrackingStore trackingStore = type switch
        {
            StoreType.MemoryTracking => new MemoryTrackingStore(new MemoryStore()),
            StoreType.PersistentTracking =>
                new PersistentTrackingStore(RocksDbUtility.OpenDb(path), new MemoryStore()),
            _ => throw new ArgumentException(nameof(type))
        };
        ReadOnlyStoreTests.PopulateTrackingStore(trackingStore);
        return trackingStore;
    }
}
