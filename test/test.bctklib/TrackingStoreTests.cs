using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using Xunit;

namespace test.bctklib;

using static Utility;
using static ReadOnlyStoreTests;
using static ReadWriteStoreTests;

public class TrackingStoreTests : IDisposable
{
    public enum StoreType { MemoryTracking, PersistentTracking }

    readonly CleanupPath path = new();

    public void Dispose()
    {
        path.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory, CombinatorialData]
    public void tracking_store_disposes_underlying_store(StoreType storeType)
    {
        var disposableStore = new DisposableStore();
        var trackingStore = GetTrackingStore(storeType, disposableStore);
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
    public void tryget_value_for_valid_key(StoreType storeType, [CombinatorialValues(-2, -1, 1, 2)] int id)
    {
        var store = GetStore(storeType);
        test_tryget_value_for_valid_key(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void contains_false_for_missing_key(StoreType storeType, [CombinatorialValues(0, 100)] int id)
    {
        var store = GetStore(storeType);
        test_contains_false_for_missing_key(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void tryget_null_for_missing_value(StoreType storeType, [CombinatorialValues(0, 100)] int id)
    {
        var store = GetStore(storeType);
        test_tryget_null_for_missing_value(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void contains_true_for_valid_key(StoreType storeType, [CombinatorialValues(-2, -1, 1, 2)] int id)
    {
        var store = GetStore(storeType);
        test_contains_true_for_valid_key(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void can_seek_forward_no_prefix(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_can_seek_forward_no_prefix(store);
    }

    [Theory, CombinatorialData]
    public void can_seek_backwards_no_prefix(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_can_seek_backwards_no_prefix(store);
    }

    [Theory, CombinatorialData]
    public void seek_forwards_with_prefix(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_seek_forwards_with_prefix(store);
    }

    [Theory, CombinatorialData]
    public void seek_backwards_with_prefix(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_seek_backwards_with_prefix(store);
    }

    [Theory, CombinatorialData]
    public void put_new_value(StoreType storeType, [CombinatorialValues(0, 100)] int id)
    {
        using var store = GetStore(storeType);
        test_put_new_value(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void put_overwrite_existing_value(StoreType storeType, [CombinatorialValues(-2, -1, 1, 2)] int id)
    {
        using var store = GetStore(storeType);
        test_put_overwrite_existing_value(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void tryget_return_null_for_deleted_key(StoreType storeType, [CombinatorialValues(-2, -1, 1, 2)] int id)
    {
        using var store = GetStore(storeType);
        test_tryget_return_null_for_deleted_key(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void contains_false_for_deleted_key(StoreType storeType, [CombinatorialValues(-2, -1, 1, 2)] int id)
    {
        using var store = GetStore(storeType);
        test_contains_false_for_deleted_key(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_add(StoreType storeType, [CombinatorialValues(0, 100)] int id)
    {
        using var store = GetStore(storeType);
        test_snapshot_commit_add(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_update(StoreType storeType, [CombinatorialValues(-2, -1, 1, 2)] int id)
    {
        using var store = GetStore(storeType);
        test_snapshot_commit_update(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_delete(StoreType storeType, [CombinatorialValues(-2, -1, 1, 2)] int id)
    {
        using var store = GetStore(storeType);
        test_snapshot_commit_delete(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void snapshot_isolation_addition(StoreType storeType, [CombinatorialValues(0, 100)] int id)
    {
        using var store = GetStore(storeType);
        test_snapshot_isolation_addition(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void snapshot_isolation_update(StoreType storeType, [CombinatorialValues(-2, -1, 1, 2)] int id)
    {
        using var store = GetStore(storeType);
        test_snapshot_isolation_update(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void snapshot_isolation_delete(StoreType storeType, [CombinatorialValues(-2, -1, 1, 2)] int id)
    {
        using var store = GetStore(storeType);
        test_snapshot_isolation_delete(store, Bytes(id));
    }

    [Theory, CombinatorialData]
    public void key_instance_isolation(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_key_instance_isolation(store);
    }

    [Theory, CombinatorialData]
    public void value_instance_isolation(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_value_instance_isolation(store);
    }

    [Theory, CombinatorialData]
    public void put_null_value_throws(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_put_null_value_throws(store);
    }

    [Theory, CombinatorialData]
    public void snapshot_put_null_value_throws(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_snapshot_put_null_value_throws(store);
    }

    [Theory, CombinatorialData]
    public void delete_missing_value_no_effect(StoreType storeType, [CombinatorialValues(0, 100)] int id)
    {
        using var store = GetStore(storeType);
        test_delete_missing_value_no_effect(store, Bytes(id));
    }

    internal IStore GetStore(StoreType type)
    {
        var memoryStore = new MemoryStore();
        var trackingStore = GetTrackingStore(type, memoryStore);

        foreach (var kvp in TestData)
        {
            var id = BitConverter.ToInt32(kvp.Key);
            if (id < 0)
            {
                // put all kvp's with a negative ID *only* in the underlying store
                memoryStore.Put(kvp.Key, kvp.Value);
            }
            else
            {
                // put an incorrect value for half the kvp's with a positive ID in the underlying store
                if (id % 2 == 0)
                {
                    memoryStore.Put(kvp.Key, Array.Empty<byte>());
                }
                // put all the correct value for all the KVPs with a positive ID in the tracking store
                trackingStore.Put(kvp.Key, kvp.Value);
            }
        }

        // put a value for Bytes(0) in the underlying store and delete it in the tracking store
        var key = Bytes(0);
        memoryStore.Put(key, Array.Empty<byte>());
        trackingStore.Delete(key);

        // keys above GreekLetters.Count have no value in either store

        return trackingStore;
    }

    IStore GetTrackingStore(StoreType storeType, IReadOnlyStore store)
        => storeType switch
        {
            StoreType.MemoryTracking => new MemoryTrackingStore(store),
            StoreType.PersistentTracking =>
                new PersistentTrackingStore(RocksDbUtility.OpenDb(path), store),
            _ => throw new ArgumentException("Unknown StoreType", nameof(storeType)),
        };

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
