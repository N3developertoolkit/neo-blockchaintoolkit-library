using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using Xunit;

namespace test.bctklib;

using static Utility;

public class TrackingStoreTests : IDisposable
{
    public enum TrackingStoreType { Memory, Persistent }


    readonly CleanupPath path = new CleanupPath();

    public void Dispose()
    {
        path.Dispose();
    }

    [Theory, CombinatorialData]
    public void disposes_underlying_store_if_disposable(TrackingStoreType storeType)
    {
        var disposableStore = new DisposableStore();
        using var trackingStore = GetStore(storeType, disposableStore);
        disposableStore.Disposed.Should().BeFalse();
        trackingStore.Dispose();
        disposableStore.Disposed.Should().BeTrue();
    }

    // index combinatorial enables test of three different scenarios:
    //  * all even indexes (including zero) have a value in the underlying store with no updates in tracking store
    //  * all odd indexes (including 1 and 5) have an updated value in the tracking store
    //  * odd indexes that are also factors of 5 (including 5) have an overwritten value in the underlying store
    //    and an updated value in the tracking store

    [Theory, CombinatorialData]
    public void tryget_value_for_valid_key(TrackingStoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.tryget_value_for_valid_key(store, index);
    }

    [Theory, CombinatorialData]
    public void tryget_null_for_missing_value(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.tryget_null_for_missing_key(store);
    }

    [Theory, CombinatorialData]
    public void tryget_null_for_deleted_key(TrackingStoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.tryget_null_for_deleted_key(store, index);
    }

    [Theory, CombinatorialData]
    public void contains_true_for_valid_key(TrackingStoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.contains_true_for_valid_key(store, index);
    }

    [Theory, CombinatorialData]
    public void contains_false_for_missing_key(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.contains_false_for_missing_key(store);
    }

    [Theory, CombinatorialData]
    public void contains_false_for_deleted_key(TrackingStoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.contains_false_for_deleted_key(store, index);
    }

    [Theory, CombinatorialData]
    public void put_new_value(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.put_new_value(store);
    }

    [Theory, CombinatorialData]
    public void put_overwrite_existing_value(TrackingStoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.put_overwrite_existing_value(store, index);
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_add(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.snapshot_commit_add(store);
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_update(TrackingStoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.snapshot_commit_update(store, index);
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_delete(TrackingStoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.snapshot_commit_delete(store, index);
    }

    [Theory, CombinatorialData]
    public void snapshot_isolation_addition(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.snapshot_isolation_addition(store);
    }

    [Theory, CombinatorialData]
    public void snapshot_isolation_update(TrackingStoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.snapshot_isolation_update(store, index);
    }

    [Theory, CombinatorialData]
    public void snapshot_isolation_delete(TrackingStoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.snapshot_isolation_delete(store, index);
    }

    [Theory, CombinatorialData]
    public void key_instance_isolation(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.key_instance_isolation(store);
    }

    [Theory, CombinatorialData]
    public void value_instance_isolation(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.value_instance_isolation(store);
    }

    [Theory, CombinatorialData]
    public void cant_put_null_value(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.put_null_value_throws(store);
    }

    [Theory, CombinatorialData]
    public void cant_put_null_value_to_snapshot(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.snapshot_put_null_value_throws(store);
    }

    [Theory, CombinatorialData]
    public void can_seek_forward_no_prefix(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.seek_forward_no_prefix(store);
    }

    [Theory, CombinatorialData]
    public void can_seek_backwards_no_prefix(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.seek_backward_no_prefix(store);
    }

    [Theory, CombinatorialData]
    public void seek_forwards_with_prefix(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.seek_forward_with_prefix(store);
    }

    [Theory, CombinatorialData]
    public void seek_backwards_with_prefix(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.seek_backward_with_prefix(store);
    }

    [Theory, CombinatorialData]
    public void delete_missing_value_no_effect(TrackingStoreType storeType)
    {
        using var store = GetPopulatedStore(storeType);
        StoreTests.delete_missing_value_no_effect(store);
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


    ITrackingStore GetStore(TrackingStoreType type, IReadOnlyStore? readOnlyStore = null)
    {
        readOnlyStore ??= new MemoryStore();
        return type switch
        {
            TrackingStoreType.Memory => new MemoryTrackingStore(readOnlyStore),
            TrackingStoreType.Persistent =>
                new PersistentTrackingStore(RocksDbUtility.OpenDb(path), readOnlyStore),
            _ => throw new ArgumentException(nameof(type))
        };
    }

    IStore GetPopulatedStore(TrackingStoreType type)
    {
        var trackingStore = GetStore(type);
        PopulateStore(trackingStore);
        return trackingStore;
    }

    void PopulateStore(ITrackingStore trackingStore)
    {
        var underlyingStore = trackingStore.UnderlyingStore as IStore;
        if (underlyingStore is null) throw new ArgumentException(nameof(trackingStore));

        var array = TestData.ToArray();
        var overwritten = Bytes("overwritten");

        for (var i = 0; i < array.Length; i++)
        {
            // put value to be overwritten to underlying store for odd, factor of five indexes
            if (i % 2 == 1 && i % 5 == 0) underlyingStore.Put(array[i].key, overwritten);

            // put value to underlying store for even indexes, tracking store for odd indexes
            var store = i % 2 == 0 ? underlyingStore : trackingStore;
            store.Put(array[i].key, array[i].value);
        }
    }
}
