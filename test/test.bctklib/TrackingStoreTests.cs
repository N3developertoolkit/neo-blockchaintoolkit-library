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
    static readonly byte[] zeroKey = Bytes(0);
    static readonly byte[] helloValue = Bytes("Hello");
    static readonly byte[] worldValue = Bytes("World");

    readonly CleanupPath path = new CleanupPath();

    public void Dispose()
    {
        path.Dispose();
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void disposes_underlying_store_if_disposable(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var disposableStore = new DisposableStore();
        using var trackingStore = storeFactory(disposableStore, path);
        disposableStore.Disposed.Should().BeFalse();
        trackingStore.Dispose();
        disposableStore.Disposed.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_get_underlying_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_get_value_from_store(store, 0);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_get_tracking_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_get_value_from_store(store, 1);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_get_overwritten_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_get_value_from_store(store, 5);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void tryget_null_for_missing_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.tryget_null_for_missing_key(store);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void tryget_null_for_deleted_underlying_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.tryget_null_for_deleted_key(store, 0);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void tryget_null_for_deleted_tracking_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.tryget_null_for_deleted_key(store, 1);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void tryget_null_for_deleted_overwritten_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.tryget_null_for_deleted_key(store, 5);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void contains_true_for_valid_underlying_key(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.contains_true_for_valid_key(store, 0);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void contains_true_for_valid_tracking_key(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.contains_true_for_valid_key(store, 1);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void contains_true_for_valid_overwritten_key(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.contains_true_for_valid_key(store, 5);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void contains_false_for_missing_key(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.contains_false_for_missing_key(store);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void contains_false_for_deleted_underlying_key(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.contains_false_for_deleted_key(store, 0);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void contains_false_for_deleted_tracking_key(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.contains_false_for_deleted_key(store, 1);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void contains_false_for_deleted_overwritten_key(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.contains_false_for_deleted_key(store, 5);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_add_new_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_add_new_value(store);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_overwrite_existing_underlying_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_overwrite_existing_value(store, 0);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_overwrite_existing_tracking_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_overwrite_existing_value(store, 0);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_overwrite_existing_overwritten_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_overwrite_existing_value(store, 0);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_add_to_store_via_snapshot(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_add_to_store_via_snapshot(store);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_update_underlying_via_snapshot(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_update_store_via_snapshot(store, 0);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_update_tracking_via_snapshot(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_update_store_via_snapshot(store, 1);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_update_overwritten_via_snapshot(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_update_store_via_snapshot(store, 5);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_delete_underlying_via_snapshot(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_delete_from_store_via_snapshot(store, 0);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_delete_tracking_via_snapshot(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_delete_from_store_via_snapshot(store, 1);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_delete_overwritten_via_snapshot(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_delete_from_store_via_snapshot(store, 5);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void update_key_array_doesnt_affect_store(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.update_key_array_doesnt_affect_store(store);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void cant_put_null_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.cant_put_null_value(store);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void cant_put_null_value_to_snapshot(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.cant_put_null_value_to_snapshot(store);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_seek_forward_no_prefix(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_seek_forward_no_prefix(store);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_seek_backwards_no_prefix(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.can_seek_backwards_no_prefix(store);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void seek_forwards_with_prefix(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.seek_forwards_with_prefix(store);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void seek_backwards_with_prefix(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekData(storeFactory);
        StoreTests.seek_backwards_with_prefix(store);
    }

    public static IEnumerable<object[]> TrackingStoreFactories()
    {
        Func<IReadOnlyStore, string, IStore> memoryFactory = (store, path) => new MemoryTrackingStore(store);
        Func<IReadOnlyStore, string, IStore> persistentFactory = (store, path) => new PersistentTrackingStore(RocksDbUtility.OpenDb(path), store);

        yield return new object[] { memoryFactory };
        yield return new object[] { persistentFactory };
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

    IStore PopulateSeekData(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var memoryStore = new MemoryStore();
        var trackingStore = storeFactory(memoryStore, path);

        var array = SeekTestData.ToArray();
        var oldData = Bytes("overwritten");

        for (var i = 0; i < array.Length; i++)
        {
            // write value to be overwritten to underlying store 
            if (i % 2 == 1 && i % 5 == 0) memoryStore.Put(array[i].key, oldData);

            // write correct value to alternating memory and tracking store
            var store = i % 2 == 0 ? memoryStore : trackingStore;
            store.Put(array[i].key, array[i].value);
        }
        return trackingStore;
    }
}
