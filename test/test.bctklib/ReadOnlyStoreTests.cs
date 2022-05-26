using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using Xunit;

namespace test.bctklib;

using static Utility;

public class ReadOnlyStoreTests : IClassFixture<CheckpointFixture>, IClassFixture<RocksDbFixture>, IDisposable
{
    public enum StoreType { Checkpoint, Memory, MemoryTracking, NeoRocksDb, PersistentTracking, RocksDb }

    readonly CheckpointFixture checkpointFixture;
    readonly RocksDbFixture rocksDbFixture;
    readonly CleanupPath path = new CleanupPath();

    public ReadOnlyStoreTests(RocksDbFixture rocksDbFixture, CheckpointFixture checkpointFixture)
    {
        this.checkpointFixture = checkpointFixture;
        this.rocksDbFixture = rocksDbFixture;
    }

    public void Dispose()
    {
        path.Dispose();
    }

    [Fact]
    public void checkpoint_cleans_up_on_dispose()
    {
        var checkpoint = new CheckpointStore(checkpointFixture.CheckpointPath);
        System.IO.Directory.Exists(checkpoint.CheckpointTempPath).Should().BeTrue();
        checkpoint.Dispose();
        System.IO.Directory.Exists(checkpoint.CheckpointTempPath).Should().BeFalse();
    }

    [Fact]
    public void checkpoint_settings()
    {
        using var store = new CheckpointStore(checkpointFixture.CheckpointPath);
        store.Settings.AddressVersion.Should().Be(checkpointFixture.AddressVersion);
        store.Settings.Network.Should().Be(checkpointFixture.Network);
    }

    [Fact]
    public void checkpoint_store_throws_on_incorrect_metadata()
    {
        Assert.Throws<Exception>(() => new CheckpointStore(checkpointFixture.CheckpointPath, network: 0));
        Assert.Throws<Exception>(() => new CheckpointStore(checkpointFixture.CheckpointPath, addressVersion: 0));
        Assert.Throws<Exception>(() => new CheckpointStore(checkpointFixture.CheckpointPath, scriptHash: Neo.UInt160.Zero));
    }

    [Fact]
    public void readonly_rocksdb_store_throws_on_write_operations()
    {
        using (var popDB = RocksDbUtility.OpenDb(path))
        {
            RocksDbFixture.Populate(popDB);
        }

        using var store = new RocksDbStore(RocksDbUtility.OpenReadOnlyDb(path), readOnly: true);
        Assert.Throws<InvalidOperationException>(() => store.Put(Bytes(0), Bytes(0)));
        Assert.Throws<InvalidOperationException>(() => store.PutSync(Bytes(0), Bytes(0)));
        Assert.Throws<InvalidOperationException>(() => store.Delete(Bytes(0)));
        Assert.Throws<InvalidOperationException>(() => store.GetSnapshot());
    }

    [Fact]
    public void rocksdb_store_sharing()
    {
        using (var popDB = RocksDbUtility.OpenDb(path))
        {
            RocksDbFixture.Populate(popDB);
        }

        using var db = RocksDbUtility.OpenDb(path);
        var column = db.GetDefaultColumnFamily();

        TestStoreSharing(shared => new RocksDbStore(db, column, readOnly: true, shared));
    }

    [Fact]
    public void persistent_tracking_store_sharing()
    {
        var memoryStore = new Neo.Persistence.MemoryStore();
        foreach (var item in TestData)
        {
            memoryStore.Put(item.key, item.value);
        }

        using var db = RocksDbUtility.OpenDb(path);
        var column = db.GetDefaultColumnFamily();

        TestStoreSharing(shared => new PersistentTrackingStore(db, column, memoryStore, shared));
    }

    [Theory, CombinatorialData]
    public void disposes_underlying_store_if_disposable(
        [CombinatorialValues(StoreType.MemoryTracking, StoreType.PersistentTracking)] StoreType storeType)
    {
        var disposableStore = new DisposableStore();
        using ITrackingStore trackingStore = storeType switch
        {
            StoreType.MemoryTracking => new MemoryTrackingStore(disposableStore),
            StoreType.PersistentTracking =>
                new PersistentTrackingStore(RocksDbUtility.OpenDb(path), disposableStore),
            _ => throw new ArgumentException(nameof(storeType)),
        };
        disposableStore.Disposed.Should().BeFalse();
        trackingStore.Dispose();
        disposableStore.Disposed.Should().BeTrue();
    }

    // index combinatorial enables test of three different scenarios:
    //  * all even indexes (including zero) have a value in the underlying store with no updates in tracking store
    //  * all odd indexes (including 1 and 5) have an updated value in the tracking store
    //  * odd indexes that are also factors of 5 (including 5) have an overwritten value in the underlying store
    //    and an updated value in the tracking store

    [SkippableTheory, CombinatorialData]
    public void tryget_value_for_valid_key(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        SkipNonTrackCombinatorial(storeType, index);
        var store = GetReadOnlyStore(storeType);
        using var _ = store as IDisposable;
        var (key, value) = TestData.ElementAt(index);
        store.TryGet(key).Should().BeEquivalentTo(value);
    }

    [Theory, CombinatorialData]
    public void tryget_null_for_missing_value(StoreType storeType)
    {
        var store = GetReadOnlyStore(storeType);
        using var _ = store as IDisposable;
        store.TryGet(Bytes(0)).Should().BeNull();
    }

    [SkippableTheory, CombinatorialData]
    public void contains_true_for_valid_key(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        SkipNonTrackCombinatorial(storeType, index);
        var store = GetReadOnlyStore(storeType);
        using var _ = store as IDisposable;
        var (key, value) = TestData.ElementAt(index);
        store.Contains(key).Should().BeTrue();
    }

    [Theory, CombinatorialData]
    public void contains_false_for_missing_key(StoreType storeType)
    {
        var store = GetReadOnlyStore(storeType);
        using var _ = store as IDisposable;
        store.Contains(Bytes(0)).Should().BeFalse();
    }

    [SkippableTheory, CombinatorialData]
    public void contains_false_for_deleted_key(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        SkipNonTrackCombinatorial(storeType, index);
        var store = GetReadOnlyStore(storeType);
        using var _ = store as IDisposable;
        store.Contains(Bytes(0)).Should().BeFalse();
    }

    [Theory, CombinatorialData]
    public void can_seek_forward_no_prefix(StoreType storeType)
    {
        var store = GetReadOnlyStore(storeType);
        using var _ = store as IDisposable;
        store.Seek(Array.Empty<byte>(), SeekDirection.Forward)
            .Should().BeEquivalentTo(TestData);
    }

    [Theory, CombinatorialData]
    public void can_seek_backwards_no_prefix(StoreType storeType)
    {
        var store = GetReadOnlyStore(storeType);
        using var _ = store as IDisposable;
        store.Seek(Array.Empty<byte>(), SeekDirection.Backward).Should().BeEmpty();
    }

    [Theory, CombinatorialData]
    public void seek_forwards_with_prefix(StoreType storeType)
    {
        var store = GetReadOnlyStore(storeType);
        using var _ = store as IDisposable;
        var key = new byte[] { 1, 0 };
        var expected = TestData
            .Where(kvp => ReadOnlyMemoryComparer.Default.Compare(kvp.key, key) >= 0);
        store.Seek(key, SeekDirection.Forward).Should().BeEquivalentTo(expected);
    }

    [Theory, CombinatorialData]
    public void seek_backwards_with_prefix(StoreType storeType)
    {
        var store = GetReadOnlyStore(storeType);
        using var _ = store as IDisposable;
        var key = new byte[] { 2, 0 };
        var expected = TestData
            .Where(kvp => ReadOnlyMemoryComparer.Reverse.Compare(kvp.key, key) >= 0)
            .Reverse();
        store.Seek(key, SeekDirection.Backward).Should().BeEquivalentTo(expected);
    }

    static void SkipNonTrackCombinatorial(StoreType storeType, int index)
        => Skip.If(!(storeType == StoreType.MemoryTracking || storeType == StoreType.PersistentTracking)
            && index > 0);

    IReadOnlyStore GetReadOnlyStore(StoreType type) => type switch
    {
        StoreType.Checkpoint => new CheckpointStore(checkpointFixture.CheckpointPath),
        StoreType.Memory => GetPopulatedMemoryStore(),
        StoreType.NeoRocksDb => CreateNeoRocksDb(rocksDbFixture.DbPath),
        StoreType.RocksDb => new RocksDbStore(RocksDbUtility.OpenDb(rocksDbFixture.DbPath), readOnly:true),
        _ => GetPopulatedTrackingStore(type),
    };

    internal static MemoryStore GetPopulatedMemoryStore()
    {
        MemoryStore memoryStore = new();
        foreach (var (key, value) in TestData)
        {
            memoryStore.Put(key, value);
        }
        return memoryStore;
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
        PopulateTrackingStore(trackingStore);
        return trackingStore;
    }

    internal static void PopulateTrackingStore(ITrackingStore trackingStore)
    {
        var memoryStore = (MemoryStore)trackingStore.UnderlyingStore;

        var array = TestData.ToArray();
        var overwritten = Bytes("overwritten");

        for (var i = 0; i < array.Length; i++)
        {
            // put value to be overwritten to underlying store for odd, factor of five indexes
            if (i % 2 == 1 && i % 5 == 0) memoryStore.Put(array[i].key, overwritten);

            // put value to underlying store for even indexes, tracking store for odd indexes
            IStore store = i % 2 == 0 ? memoryStore : trackingStore;
            store.Put(array[i].key, array[i].value);
        }
    }

    static void TestStoreSharing(Func<bool, IStore> storeFactory)
    {
        var (key, value) = TestData.First();

        // create shared, doesn't dispose underlying rocksDB when store disposed
        var store1 = storeFactory(true);
        store1.TryGet(key).Should().BeEquivalentTo(value);
        store1.Dispose();

        // create unshared, disposes underlying rocksDB when store disposed
        var store2 = storeFactory(false);
        store2.TryGet(key).Should().BeEquivalentTo(value);
        store2.Dispose();
        Assert.Throws<ObjectDisposedException>(() => store2.TryGet(key));

        // since underlying rocksDB is disposed, attempting to call methods
        // on a new instance will throw
        using var store3 = storeFactory(true);
        Assert.Throws<ObjectDisposedException>(() => store3.TryGet(key));
    }

    class DisposableStore : IReadOnlyStore, IDisposable
    {
        public bool Disposed { get; private set; } = false;

        public void Dispose()
        {
            Disposed = true;
        }

        byte[]? IReadOnlyStore.TryGet(byte[]? key) => null;
        bool IReadOnlyStore.Contains(byte[]? key) => false;
        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte[]? key, SeekDirection direction)
            => Enumerable.Empty<(byte[], byte[])>();
    }
}
