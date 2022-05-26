using System;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public static partial class RocksDbUtility
    {
        public struct PinnableSlice : IDisposable
        {
            public IntPtr Handle { get; internal set; }

            public PinnableSlice(IntPtr handle)
            {
                Handle = handle;
            }

            public bool Valid => Handle != IntPtr.Zero;

            public unsafe ReadOnlySpan<byte> GetValue()
            {
                if (Handle == IntPtr.Zero) return default;
                var valuePtr = Native.Instance.rocksdb_pinnableslice_value(Handle, out var valueLength);
                if (valuePtr == IntPtr.Zero) return default;
                return new ReadOnlySpan<byte>((byte*)valuePtr, (int)valueLength);
            }

            public void Dispose()
            {
                if (Handle != IntPtr.Zero)
                {
                    var handle = Handle;
                    Handle = IntPtr.Zero;
                    Native.Instance.rocksdb_pinnableslice_destroy(handle);
                }
            }
        }
    }
}