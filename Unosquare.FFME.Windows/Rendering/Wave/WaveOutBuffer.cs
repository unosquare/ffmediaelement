namespace Unosquare.FFME.Rendering.Wave
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A buffer of Wave samples for streaming to a Wave Output device
    /// </summary>
    internal class WaveOutBuffer : IDisposable
    {
        private readonly WaveHeader header;
        private readonly byte[] Buffer;
        private readonly IWaveProvider WaveStream;

        private IntPtr DeviceHandle;
        private GCHandle BufferHandle;
        private GCHandle HeaderHandle; // we need to pin the header structure

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
        /// Finalizes an instance of the <see cref="WaveOutBuffer"/> class.
        /// </summary>
        ~WaveOutBuffer()
        {
            Dispose(false);
            System.Diagnostics.Debug.Assert(true, $"{nameof(WaveOutBuffer)} was not disposed");
        }

        /// <summary>
        /// Whether the header's in queue flag is set
        /// </summary>
        public bool IsQueued => header.Flags.HasFlag(WaveHeaderFlags.InQueue);

        /// <summary>
        /// The buffer size in bytes
        /// </summary>
        public int BufferSize { get; }

        /// <summary>
        /// Releases resources held by this WaveBuffer
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
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

        /// <summary>
        /// Releases resources held by this WaveBuffer
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected void Dispose(bool alsoManaged)
        {
            if (alsoManaged)
            {
                // free managed resources
            }

            if (HeaderHandle.IsAllocated)
                HeaderHandle.Free();

            if (BufferHandle.IsAllocated)
                BufferHandle.Free();

            if (DeviceHandle != IntPtr.Zero)
            {
                WaveInterop.ReleaseHeader(DeviceHandle, header);
                DeviceHandle = IntPtr.Zero;
            }
        }
    }
}
