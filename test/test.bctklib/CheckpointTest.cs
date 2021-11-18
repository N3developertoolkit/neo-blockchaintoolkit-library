using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using Xunit;
using static System.Text.Encoding;

namespace test.bctklib3
{
    public class CheckpointTest : IClassFixture<CheckpointFixture>
    {
        readonly CheckpointFixture fixture;
        public CheckpointTest(CheckpointFixture fixture)
        {
            this.fixture = fixture;
        }

        static byte[] Bytes(params byte[] values) => values;
        static byte[] Bytes(int value) => BitConverter.GetBytes(value);
        static byte[] Bytes(string value) => UTF8.GetBytes(value);


        [Fact]
        public void checkpoint_cleans_up_on_dispose()
        {
            var checkpoint = new CheckpointStore(fixture.CheckpointPath);
            Directory.Exists(checkpoint.checkpointTempPath).Should().BeTrue();
            checkpoint.Dispose();
            Directory.Exists(checkpoint.checkpointTempPath).Should().BeFalse();
        }

        [Fact]
        public void checkpoint_settings()
        {
            using var store = new CheckpointStore(fixture.CheckpointPath);
            store.Settings.AddressVersion.Should().Be(fixture.AddressVersion);
            store.Settings.Network.Should().Be(fixture.Network);
        }

        [Fact]
        public void can_get_value_from_store()
        {
            var (key, value) = CheckpointFixture.GetSeekData().First();

            using var store = new CheckpointStore(fixture.CheckpointPath);
            store.Contains(key).Should().BeTrue();
            store.TryGet(key).Should().BeEquivalentTo(value);
        }

        [Fact]
        public void contains_false_for_missing_key()
        {
            using var store = new CheckpointStore(fixture.CheckpointPath);
            store.Contains(Bytes("invalid-key")).Should().BeFalse();
        }

        [Fact]
        public void can_seek_forward_no_prefix()
        {
            using var store = new CheckpointStore(fixture.CheckpointPath);

            var actual = store.Seek(Array.Empty<byte>(), SeekDirection.Forward);
            var expected = CheckpointFixture.GetSeekData();
            actual.Should().BeEquivalentTo(expected);
        }

        [Fact(Skip = "https://github.com/neo-project/neo/issues/2634")]
        public void can_seek_backwards_no_prefix()
        {
            using var store = new CheckpointStore(fixture.CheckpointPath);
            var actual = store.Seek(Array.Empty<byte>(), SeekDirection.Backward).ToArray();
            var expected = CheckpointFixture.GetSeekData().Reverse();
            actual.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void seek_forwards_with_prefix()
        {
            using var store = new CheckpointStore(fixture.CheckpointPath);

            var actual = store.Seek(Bytes(1), SeekDirection.Forward);
            var expected = CheckpointFixture.GetSeekData().Where(kvp => kvp.Item1[0] >= 0x01);
            actual.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void seek_backwards_with_prefix()
        {
            using var store = new CheckpointStore(fixture.CheckpointPath);

            var actual = store.Seek(Bytes(2), SeekDirection.Backward);
            var expected = CheckpointFixture.GetSeekData().Where(kvp => kvp.Item1[0] <= 0x01).Reverse();
            actual.Should().BeEquivalentTo(expected);
        }
    }
}
