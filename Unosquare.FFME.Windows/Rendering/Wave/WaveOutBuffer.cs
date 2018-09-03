namespace Unosquare.FFME.Rendering.Wave
{
    using Primitives;
    using System;
    using System.Runtime.InteropServices;

    /// <inheritdoc/>
    /// <summary>
    /// A buffer of Wave samples for streaming to a Wave Output device
    /// </summary>
    internal sealed class WaveOutBuffer : IDisposable
    {
        private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);
        private readonly WaveHeader header;
        private readonly byte[] Buffer;
        private readonly IWaveProvider WaveStream;

        // Structs
        private GCHandle BufferHandle;
        private GCHandle HeaderHandle; // we need to pin the header structure
        private IntPtr DeviceHandle;

        /// <summary>
        /// Initializes a new instance of the <see cref="WaveOutBuffer"/> class.
        /// </summary>
        /// <param name="deviceHandle">WaveOut device to write to</param>
        /// <param name="bufferSize">Buffer size in bytes</param>
        /// <param name="waveStream">Stream to provide more data</param>
        public WaveOutBuffer(IntPtr deviceHandle, int bufferSize, IWaveProvider waveStream)
        {
            BufferSize = bufferSize;
            Buffer = new byte[BufferSize];
            DeviceHandle = deviceHandle;
            WaveStream = waveStream;

            BufferHandle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
            header = new WaveHeader();
            HeaderHandle = GCHandle.Alloc(header, GCHandleType.Pinned);

            header.DataBuffer = BufferHandle.AddrOfPinnedObject();
            header.BufferLength = bufferSize;
            header.Loops = 1;
            header.UserData = IntPtr.Zero;

            WaveInterop.AllocateHeader(DeviceHandle, header);
        }

        /// <summary>
        /// Whether the header's in queue flag is set
        /// </summary>
        public bool IsQueued => header.Flags.HasFlag(WaveHeaderFlags.InQueue);

        /// <summary>
        /// The buffer size in bytes
        /// </summary>
        public int BufferSize { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            if (m_IsDisposed.Value) return;
            m_IsDisposed.Value = true;

            // Release the wave header
            WaveInterop.ReleaseHeader(DeviceHandle, header);

            // Unpin The header
            if (HeaderHandle.IsAllocated)
                HeaderHandle.Free();

            // Unpin the buffer
            if (BufferHandle.IsAllocated)
                BufferHandle.Free();

            // Reset the struct fields
            HeaderHandle = default;
            BufferHandle = default;
            DeviceHandle = IntPtr.Zero;
        }

        /// <summary>
        /// Clears the internal buffer data.
        /// </summary>
        public void Clear()
        {
            if (Buffer != null)
                Array.Clear(Buffer, 0, Buffer.Length);
        }

        /// <summary>
        /// this is called by the Wave callback and should be used to refill the buffer.
        /// This calls the <see cref="IWaveProvider.Read(byte[], int, int)"/> method on the stream
        /// </summary>
        /// <returns>True when bytes were written. False if no bytes were written.</returns>
        internal bool ReadWaveStream()
        {
            var readCount = WaveStream.Read(Buffer, 0, Buffer.Length);
            if (readCount <= 0) return false;

            if (readCount < Buffer.Length)
                Array.Clear(Buffer, readCount, Buffer.Length - readCount);

            WaveInterop.WriteAudioData(DeviceHandle, header);
            return true;
        }
    }
}
