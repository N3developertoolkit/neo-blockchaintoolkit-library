using System;
using System.Linq;
using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using Xunit;

namespace test.bctklib;

using static Utility;

public static class StoreTests
{
    public static void can_get_value_from_store(IReadOnlyStore store, int index = 0)
    {
        var (key, value) = SeekTestData.ElementAt(index);
        store.TryGet(key).Should().BeEquivalentTo(value);
    }

    public static void tryget_null_for_missing_key(IReadOnlyStore store)
    {
        store.TryGet(Bytes(0)).Should().BeNull();
    }

    public static void tryget_null_for_deleted_key(IStore store, int index = 0)
    {
        var (key, _) = SeekTestData.ElementAt(index);
        store.TryGet(key).Should().NotBeNull();
        store.Delete(key);
        store.TryGet(key).Should().BeNull();
    }

    public static void contains_true_for_valid_key(IReadOnlyStore store, int index = 0)
    {
        var (key, value) = SeekTestData.ElementAt(index);
        store.Contains(key).Should().BeTrue();
    }

    public static void contains_false_for_missing_key(IReadOnlyStore store)
    {
        store.Contains(Bytes(0)).Should().BeFalse();
    }

    public static void contains_false_for_deleted_key(IStore store, int index = 0)
    {
        var (key, value) = SeekTestData.ElementAt(index);
        store.Contains(key).Should().BeTrue();
        store.Delete(key);
        store.Contains(key).Should().BeFalse();
    }

    public static void can_seek_forward_no_prefix(IReadOnlyStore store)
    {
        store.Seek(Array.Empty<byte>(), SeekDirection.Forward)
            .Should().BeEquivalentTo(SeekTestData);
    }

    public static void can_seek_backwards_no_prefix(IReadOnlyStore store)
    {
        store.Seek(Array.Empty<byte>(), SeekDirection.Backward).Should().BeEmpty();
    }

    public static void seek_forwards_with_prefix(IReadOnlyStore store)
    {
        var key = new byte[] { 1, 0 };
        var expected = SeekTestData
            .Where(kvp => ReadOnlyMemoryComparer.Default.Compare(kvp.key, key) >= 0);
        store.Seek(key, SeekDirection.Forward).Should().BeEquivalentTo(expected);
    }

    public static void seek_backwards_with_prefix(IReadOnlyStore store)
    {
        var key = new byte[] { 2, 0 };
        var expected = SeekTestData
            .Where(kvp => ReadOnlyMemoryComparer.Reverse.Compare(kvp.key, key) >= 0)
            .Reverse();
        store.Seek(key, SeekDirection.Backward).Should().BeEquivalentTo(expected);
    }

    public static void can_add_new_value(IStore store, int index = 0)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");
        store.TryGet(key).Should().BeNull();
        store.Put(key, value);
        store.TryGet(key).Should().BeEquivalentTo(value);
    }

    public static void can_overwrite_existing_value(IStore store, int index = 0)
    {
        var (key, value) = SeekTestData.ElementAt(index);
        var newValue = Bytes("test-value");
        store.TryGet(key).Should().BeEquivalentTo(value);
        store.Put(key, newValue);
        store.TryGet(key).Should().BeEquivalentTo(newValue);
    }

    public static void cant_put_null_value(IStore store)
    {
        var key = Bytes(0);
        Assert.Throws<NullReferenceException>(() => store.Put(key, null));
    }

    public static void snapshot_doesnt_see_store_addition(IStore store)
    {
        using var snapshot = store.GetSnapshot();
        var key = Bytes(0);
        var newValue = Bytes("test-value");
        store.Put(key, newValue);
        snapshot.Contains(key).Should().BeFalse();
        snapshot.TryGet(key).Should().BeNull();
    }

    public static void snapshot_doesnt_see_store_update(IStore store, int index = 0)
    {
        using var snapshot = store.GetSnapshot();
        var (key, value) = SeekTestData.ElementAt(index);
        var newValue = Bytes("test-value");
        store.Put(key, newValue);
        snapshot.Contains(key).Should().BeTrue();
        snapshot.TryGet(key).Should().BeEquivalentTo(value);
    }

    public static void snapshot_doesnt_see_store_delete(IStore store, int index = 0)
    {
        using var snapshot = store.GetSnapshot();
        var (key, value) = SeekTestData.ElementAt(index);
        store.Delete(key);
        snapshot.Contains(key).Should().BeTrue();
        snapshot.TryGet(key).Should().BeEquivalentTo(value);
    }

    public static void can_add_to_store_via_snapshot(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, value);

        store.TryGet(key).Should().BeNull();
        snapshot.Commit();
        store.TryGet(key).Should().BeEquivalentTo(value);
    }

    public static void can_update_store_via_snapshot(IStore store, int index = 0)
    {
        var (key, value) = SeekTestData.ElementAt(index);
        var newValue = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, newValue);

        store.TryGet(key).Should().BeEquivalentTo(value);
        snapshot.Commit();
        store.TryGet(key).Should().BeEquivalentTo(newValue);
    }

    public static void can_delete_from_store_via_snapshot(IStore store, int index = 0)
    {
        var (key, value) = SeekTestData.ElementAt(index);
        using var snapshot = store.GetSnapshot();
        snapshot.Delete(key);

        store.TryGet(key).Should().BeEquivalentTo(value);
        snapshot.Commit();
        store.TryGet(key).Should().BeNull();
    }

    public static void cant_put_null_value_to_snapshot(IStore store)
    {
        using var snapshot = store.GetSnapshot();
        var key = Bytes(0);
        Assert.Throws<NullReferenceException>(() => snapshot.Put(key, null));
    }

    public static void update_key_array_doesnt_affect_store(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");
        store.Put(key, value);

        key[0] = 0xff;
        store.TryGet(Bytes(0)).Should().BeEquivalentTo(value);
        store.TryGet(key).Should().BeNull();
    }
}
