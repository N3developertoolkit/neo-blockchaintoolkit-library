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
    public static void tryget_value_for_valid_key(IReadOnlyStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);
        store.TryGet(key).Should().BeEquivalentTo(value);
    }

    public static void tryget_null_for_missing_key(IReadOnlyStore store)
    {
        store.TryGet(Bytes(0)).Should().BeNull();
    }

    public static void tryget_null_for_deleted_key(IStore store, int index = 0)
    {
        var (key, _) = TestData.ElementAt(index);
        store.TryGet(key).Should().NotBeNull();
        store.Delete(key);
        store.TryGet(key).Should().BeNull();
    }

    public static void contains_true_for_valid_key(IReadOnlyStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);
        store.Contains(key).Should().BeTrue();
    }

    public static void contains_false_for_missing_key(IReadOnlyStore store)
    {
        store.Contains(Bytes(0)).Should().BeFalse();
    }

    public static void contains_false_for_deleted_key(IStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);
        store.Contains(key).Should().BeTrue();
        store.Delete(key);
        store.Contains(key).Should().BeFalse();
    }

    public static void seek_forward_no_prefix(IReadOnlyStore store)
    {
        store.Seek(Array.Empty<byte>(), SeekDirection.Forward)
            .Should().BeEquivalentTo(TestData);
    }

    public static void seek_backward_no_prefix(IReadOnlyStore store)
    {
        store.Seek(Array.Empty<byte>(), SeekDirection.Backward).Should().BeEmpty();
    }

    public static void seek_forward_with_prefix(IReadOnlyStore store)
    {
        var key = new byte[] { 1, 0 };
        var expected = TestData
            .Where(kvp => ReadOnlyMemoryComparer.Default.Compare(kvp.key, key) >= 0);
        store.Seek(key, SeekDirection.Forward).Should().BeEquivalentTo(expected);
    }

    public static void seek_backward_with_prefix(IReadOnlyStore store)
    {
        var key = new byte[] { 2, 0 };
        var expected = TestData
            .Where(kvp => ReadOnlyMemoryComparer.Reverse.Compare(kvp.key, key) >= 0)
            .Reverse();
        store.Seek(key, SeekDirection.Backward).Should().BeEquivalentTo(expected);
    }

    public static void put_new_value(IStore store, int index = 0)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");
        store.TryGet(key).Should().BeNull();
        store.Put(key, value);
        store.TryGet(key).Should().BeEquivalentTo(value);
    }

    public static void put_overwrite_existing_value(IStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);
        var newValue = Bytes("test-value");
        store.TryGet(key).Should().BeEquivalentTo(value);
        store.Put(key, newValue);
        store.TryGet(key).Should().BeEquivalentTo(newValue);
    }

    public static void put_null_value_throws(IStore store)
    {
        var key = Bytes(0);
        Assert.Throws<NullReferenceException>(() => store.Put(key, null));
    }

    // delete existing value tested by other TryGet & Contains tests
    public static void delete_missing_value_no_effect(IStore store)
    {
        var key = Bytes(0);
        store.TryGet(key).Should().BeNull();
        store.Delete(key);
        store.TryGet(key).Should().BeNull();
    }

    public static void snapshot_isolation_addition(IStore store)
    {
        using var snapshot = store.GetSnapshot();
        var key = Bytes(0);
        var newValue = Bytes("test-value");
        store.Put(key, newValue);
        snapshot.Contains(key).Should().BeFalse();
        snapshot.TryGet(key).Should().BeNull();
    }

    public static void snapshot_isolation_update(IStore store, int index = 0)
    {
        using var snapshot = store.GetSnapshot();
        var (key, value) = TestData.ElementAt(index);
        var newValue = Bytes("test-value");
        store.Put(key, newValue);
        snapshot.Contains(key).Should().BeTrue();
        snapshot.TryGet(key).Should().BeEquivalentTo(value);
    }

    public static void snapshot_isolation_delete(IStore store, int index = 0)
    {
        using var snapshot = store.GetSnapshot();
        var (key, value) = TestData.ElementAt(index);
        store.Delete(key);
        snapshot.Contains(key).Should().BeTrue();
        snapshot.TryGet(key).Should().BeEquivalentTo(value);
    }

    public static void snapshot_commit_add(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, value);

        store.TryGet(key).Should().BeNull();
        snapshot.Commit();
        store.TryGet(key).Should().BeEquivalentTo(value);
    }

    public static void snapshot_commit_update(IStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);
        var newValue = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, newValue);

        store.TryGet(key).Should().BeEquivalentTo(value);
        snapshot.Commit();
        store.TryGet(key).Should().BeEquivalentTo(newValue);
    }

    public static void snapshot_commit_delete(IStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);
        using var snapshot = store.GetSnapshot();
        snapshot.Delete(key);

        store.TryGet(key).Should().BeEquivalentTo(value);
        snapshot.Commit();
        store.TryGet(key).Should().BeNull();
    }

    public static void snapshot_put_null_value_throws(IStore store)
    {
        using var snapshot = store.GetSnapshot();
        var key = Bytes(0);
        Assert.Throws<NullReferenceException>(() => snapshot.Put(key, null));
    }

    // key/value_instance_isolation tests ensure the store has it's own copies
    // of keys and values so that changes to provided instances do not causes
    // changes in the underlying store. This is important as some of the IStore
    // implementations store keys & values in memory where they could be subject
    // to changes if copies aren't made
 
    public static void key_instance_isolation(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");
        store.Put(key, value);

        key[0] = 0xff;
        store.TryGet(Bytes(0)).Should().BeEquivalentTo(value);
        store.TryGet(key).Should().BeNull();
    }

    public static void value_instance_isolation(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");
        store.Put(key, value);

        value[0] = 0xff;
        store.TryGet(key).Should().BeEquivalentTo(Bytes("test-value"));
    }

}
