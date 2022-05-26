using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using RocksDbSharp;
using Xunit;

namespace test.bctklib;

using static Utility;

public class RocksDbStoreTest : IDisposable
{
    static readonly byte[] zeroKey = Bytes(0);
    static readonly byte[] helloValue = Bytes("Hello");
    static readonly byte[] worldValue = Bytes("World");

    readonly CleanupPath path = new CleanupPath();

    public void Dispose()
    {
        path.Dispose();
    }

    [Fact]
    public void readonly_store_throws_on_write_operations()
    {
        Populate(path);

        using var store = new RocksDbStore(RocksDbUtility.OpenReadOnlyDb(path), readOnly: true);
        Assert.Throws<InvalidOperationException>(() => store.Put(zeroKey, helloValue));
        Assert.Throws<InvalidOperationException>(() => store.PutSync(zeroKey, helloValue));
        Assert.Throws<InvalidOperationException>(() => store.Delete(zeroKey));
        Assert.Throws<InvalidOperationException>(() => store.GetSnapshot());
    }

    [Fact]
    public void store_sharing()
    {
        Populate(path, (zeroKey, helloValue));

        using var db = RocksDbUtility.OpenReadOnlyDb(path);
        var cf = db.GetDefaultColumnFamily();

        using (var store = new RocksDbStore(db, cf, readOnly: true, shared: true))
        {
            store.TryGet(zeroKey).Should().BeEquivalentTo(helloValue);
        }

        using (var store = new RocksDbStore(db, cf, readOnly: true, shared: false))
        {
            store.TryGet(zeroKey).Should().BeEquivalentTo(helloValue);
        }

        using (var store = new RocksDbStore(db, cf, readOnly: true, shared: false))
        {
            Assert.Throws<ObjectDisposedException>(() => store.TryGet(zeroKey));
        }
    }

    [Fact]
    public void can_get_value_from_store()
    {
        Populate(path, (zeroKey, helloValue));

        using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
        store.TryGet(zeroKey).Should().BeEquivalentTo(helloValue);
    }


    [Fact]
    public void tryget_null_for_invalid_key()
    {
        Populate(path);

        using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
        store.TryGet(zeroKey).Should().BeNull();
    }

    [Fact]
    public void contains_true_for_valid_key()
    {
        Populate(path, (zeroKey, helloValue));

        using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
        store.Contains(zeroKey).Should().BeTrue();
    }

    [Fact]
    public void contains_false_for_missing_key()
    {
        Populate(path);

        using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
        store.Contains(zeroKey).Should().BeFalse();
    }

    [Fact]
    public void contains_false_for_deleted_key()
    {
        Populate(path, (zeroKey, helloValue));

        using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
        store.Contains(zeroKey).Should().BeTrue();
        store.Delete(zeroKey);
        store.Contains(zeroKey).Should().BeFalse();
    }

    [Fact]
    public void can_overwrite_existing_value()
    {
        Populate(path, (zeroKey, helloValue));

        using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
        store.Put(zeroKey, worldValue);
        store.TryGet(zeroKey).Should().BeEquivalentTo(worldValue);
    }

    [Fact]
    public void can_delete_existing_value()
    {
        Populate(path, (zeroKey, helloValue));

        using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
        store.TryGet(zeroKey).Should().NotBeNull();
        store.Delete(zeroKey);
        store.TryGet(zeroKey).Should().BeNull();
    }

    [Fact]
    public void cant_put_null_value()
    {
        using var path = new CleanupPath();

        Populate(path);

        using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
        var key = Bytes(0);
        Assert.Throws<NullReferenceException>(() => store.Put(key, null));
    }

    [Fact]
    public void snapshot_doesnt_see_store_changes()
    {
        using var path = new CleanupPath();
        var key = Bytes(0);
        var hello = Bytes("hello");

        Populate(path);

        using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
        using var snapshot = store.GetSnapshot();

        store.Put(key, hello);
        snapshot.Contains(key).Should().BeFalse();
        snapshot.TryGet(key).Should().BeNull();
    }

    [Fact]
    public void can_update_store_via_snapshot()
    {
        using var path = new CleanupPath();
        var key = Bytes(0);
        var hello = Bytes("hello");

        Populate(path);

        using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
        using var snapshot = store.GetSnapshot();

        snapshot.Put(key, hello);

        store.TryGet(key).Should().BeNull();
        snapshot.Commit();
        store.TryGet(key).Should().BeEquivalentTo(hello);
    }

    [Fact]
    public void cant_put_null_value_to_snapshot()
    {
        using var path = new CleanupPath();
        Populate(path);

        using var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
        using var snapshot = store.GetSnapshot();

        var key = Bytes(0);
        Assert.Throws<NullReferenceException>(() => snapshot.Put(key, null));
    }


    [Fact]
    public void can_seek_forward_no_prefix()
    {
        using var path = new CleanupPath();
        using var store = GetSeekStore(path);

        var actual = store.Seek(Array.Empty<byte>(), SeekDirection.Forward);
        var expected = GetSeekData();
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void can_seek_backwards_no_prefix()
    {
        using var path = new CleanupPath();
        using var store = GetSeekStore(path);

        var actual = store.Seek(Array.Empty<byte>(), SeekDirection.Backward).ToArray();
        actual.Should().BeEmpty();
    }

    [Fact]
    public void seek_forwards_with_prefix()
    {
        using var path = new CleanupPath();
        using var store = GetSeekStore(path);

        var actual = store.Seek(Bytes(1), SeekDirection.Forward);
        var expected = GetSeekData().Where(kvp => kvp.Item1[0] >= 0x01);
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void seek_backwards_with_prefix()
    {
        using var path = new CleanupPath();
        using var store = GetSeekStore(path);

        var actual = store.Seek(Bytes(2), SeekDirection.Backward);
        var expected = GetSeekData().Where(kvp => kvp.Item1[0] <= 0x01).Reverse();
        actual.Should().BeEquivalentTo(expected);
    }

        static void Populate(string path, params (byte[] key, byte[] value)[] values)
    {
        using var db = RocksDbUtility.OpenDb(path);
        var cf = db.GetDefaultColumnFamily();
        var writeOptions = new WriteOptions().SetSync(true);
        for (int i = 0; i < values.Length; i++)
        {
            var (key, value) = values[i];
            db.Put(key, value, cf, writeOptions);
        }
    }

    static IStore GetSeekStore(string path)
    {
        Populate(path);
        var store = new RocksDbStore(RocksDbUtility.OpenDb(path), readOnly: false);
        store.PutSeekData((0, 2), (1, 4));
        return store;
    }

    static IEnumerable<(byte[], byte[])> GetSeekData() => Utility.GetSeekData((0, 2), (1, 4));


}
