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
    public void can_get_value_from_underlying_store(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var memoryStore = new MemoryStore();
        memoryStore.Put(zeroKey, helloValue);
        using var trackingStore = storeFactory(memoryStore, path);
        trackingStore.TryGet(zeroKey).Should().BeEquivalentTo(helloValue);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_get_value_from_tracking_store(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var trackingStore = storeFactory(NullStore.Instance, path);
        trackingStore.Put(zeroKey, helloValue);
        trackingStore.TryGet(zeroKey).Should().BeEquivalentTo(helloValue);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_get_updated_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var memoryStore = new MemoryStore();
        memoryStore.Put(zeroKey, helloValue);
        using var trackingStore = storeFactory(memoryStore, path);
        trackingStore.Put(zeroKey, worldValue);
        trackingStore.TryGet(zeroKey).Should().BeEquivalentTo(worldValue);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void tryget_null_for_missing_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var trackingStore = storeFactory(NullStore.Instance, path);
        trackingStore.TryGet(zeroKey).Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void tryget_null_for_deleted_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var memoryStore = new MemoryStore();
        memoryStore.Put(zeroKey, helloValue);
        using var trackingStore = storeFactory(memoryStore, path);
        trackingStore.Delete(zeroKey);
        trackingStore.TryGet(zeroKey).Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void contains_true_for_valid_key_in_underlying_store(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var memoryStore = new MemoryStore();
        memoryStore.Put(zeroKey, helloValue);
        using var trackingStore = storeFactory(memoryStore, path);
        trackingStore.Contains(zeroKey).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void contains_true_for_valid_key_in_tracking_store(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var trackingStore = storeFactory(NullStore.Instance, path);
        trackingStore.Put(zeroKey, helloValue);
        trackingStore.Contains(zeroKey).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void contains_false_for_missing_key(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var trackingStore = storeFactory(NullStore.Instance, path);
        trackingStore.Contains(zeroKey).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void contains_false_for_deleted_key(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var memoryStore = new MemoryStore();
        memoryStore.Put(zeroKey, helloValue);
        using var trackingStore = storeFactory(memoryStore, path);
        trackingStore.Delete(zeroKey);
        trackingStore.Contains(zeroKey).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_update_store_via_snapshot(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var memoryStore = new MemoryStore();
        memoryStore.Put(zeroKey, helloValue);
        using var trackingStore = storeFactory(memoryStore, path);
        trackingStore.TryGet(zeroKey).Should().BeEquivalentTo(helloValue);

        using var snapshot = trackingStore.GetSnapshot();
        snapshot.Put(zeroKey, worldValue);
        snapshot.Commit();
        trackingStore.TryGet(zeroKey).Should().BeEquivalentTo(worldValue);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_delete_via_snapshot(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var memoryStore = new MemoryStore();
        memoryStore.Put(zeroKey, helloValue);
        using var trackingStore = storeFactory(memoryStore, path);

        using var snapshot = trackingStore.GetSnapshot();
        snapshot.Delete(zeroKey);
        snapshot.Commit();
        trackingStore.TryGet(zeroKey).Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void update_key_array_doesnt_affect_store(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var key = Bytes(0);

        using var trackingStore = storeFactory(NullStore.Instance, path);
        trackingStore.Put(key, helloValue);

        key[0] = 0xff;
        trackingStore.TryGet(zeroKey).Should().BeEquivalentTo(helloValue);
        trackingStore.TryGet(key).Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void cant_put_null_value(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var trackingStore = storeFactory(NullStore.Instance, path);
        Assert.Throws<NullReferenceException>(() => trackingStore.Put(zeroKey, null));
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void cant_put_null_value_to_snapshot(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var trackingStore = storeFactory(NullStore.Instance, path);
        using var snapshot = trackingStore.GetSnapshot();
        Assert.Throws<NullReferenceException>(() => snapshot.Put(zeroKey, null));
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_seek_forward_no_prefix(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekTestStore(storeFactory);
        var actual = store.Seek(Array.Empty<byte>(), SeekDirection.Forward);
        actual.Should().BeEquivalentTo(TestSeekData);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void can_seek_backwards_no_prefix(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        using var store = PopulateSeekTestStore(storeFactory);
        var actual = store.Seek(Array.Empty<byte>(), SeekDirection.Backward);
        actual.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void seek_forwards_with_prefix(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var key = new byte[] { 1, 0 };
        var comparer = ReadOnlyMemoryComparer.Default;
        using var store = PopulateSeekTestStore(storeFactory);
        var actual = store.Seek(key, SeekDirection.Forward);
        var expected = TestSeekData
            .Where(kvp => ReadOnlyMemoryComparer.Default.Compare(kvp.key, key) >= 0);
        actual.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [MemberData(nameof(TrackingStoreFactories))]
    public void seek_backwards_with_prefix(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var key = new byte[] { 2, 0 };
        using var store = PopulateSeekTestStore(storeFactory);
        var actual = store.Seek(key, SeekDirection.Backward);
        var expected = TestSeekData
            .Where(kvp => ReadOnlyMemoryComparer.Reverse.Compare(kvp.key, key) >= 0)
            .Reverse();
        actual.Should().BeEquivalentTo(expected);
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

    IStore PopulateSeekTestStore(Func<IReadOnlyStore, string, IStore> storeFactory)
    {
        var memoryStore = new MemoryStore();
        var trackingStore = storeFactory(memoryStore, path);

        var array = TestSeekData.ToArray();
        var oldData = Bytes(0);

        for (var i = 0; i < array.Length; i++)
        {
            // write something unexpected to underlying store 
            memoryStore.Put(array[i].key, oldData);

            // write correct value to alternating memory and tracking store
            var store = i % 2 == 0 ? memoryStore : trackingStore;
            store.Put(array[i].key, array[i].value);
        }
        return trackingStore;
    }
}
