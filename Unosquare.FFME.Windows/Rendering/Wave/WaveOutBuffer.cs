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
        private readonly object SyncLock;

        private IntPtr DeviceHandle;
        private GCHandle BufferHandle;
        private GCHandle HeaderHandle; // we need to pin the header structure

        /// <summary>
        /// Initializes a new instance of the <see cref="WaveOutBuffer"/> class.
        /// </summary>
        /// <param name="deviceHandle">WaveOut device to write to</param>
        /// <param name="bufferSize">Buffer size in bytes</param>
        /// <param name="waveStream">Stream to provide more data</param>
        /// <param name="waveOutLock">Lock to protect WaveOut API's from being called on &gt;1 thread</param>
        public WaveOutBuffer(IntPtr deviceHandle, int bufferSize, IWaveProvider waveStream, object waveOutLock)
        {
            BufferSize = bufferSize;
            Buffer = new byte[BufferSize];
            DeviceHandle = deviceHandle;
            WaveStream = waveStream;
            SyncLock = waveOutLock;

            BufferHandle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
            header = new WaveHeader();
            HeaderHandle = GCHandle.Alloc(header, GCHandleType.Pinned);

            header.DataBuffer = BufferHandle.AddrOfPinnedObject();
            header.BufferLength = bufferSize;
            header.Loops = 1;
            header.UserData = IntPtr.Zero;

            lock (SyncLock)
            {
                MmException.Try(WaveInterop.NativeMethods.waveOutPrepareHeader(DeviceHandle, header, Marshal.SizeOf(header)),
                    nameof(WaveInterop.NativeMethods.waveOutPrepareHeader));
            }
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
        public bool InQueue => header.Flags.HasFlag(WaveHeaderFlags.InQueue);

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
        /// <returns>true when bytes were written. False if no bytes were written.</returns>
        internal bool OnDone()
        {
            int bytes;
            lock (WaveStream)
            {
                bytes = WaveStream.Read(Buffer, 0, Buffer.Length);
            }

            if (bytes == 0)
                return false;

            for (int n = bytes; n < Buffer.Length; n++)
                Buffer[n] = 0;

            WriteToWaveOut();
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
                lock (SyncLock)
                    WaveInterop.NativeMethods.waveOutUnprepareHeader(DeviceHandle, header, Marshal.SizeOf(header));

                DeviceHandle = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Writes to wave out.
        /// </summary>
        /// <exception cref="MmException">waveOutWrite</exception>
        private void WriteToWaveOut()
        {
            MmResult result;

            lock (SyncLock)
                result = WaveInterop.NativeMethods.waveOutWrite(DeviceHandle, header, Marshal.SizeOf(header));

            if (result != MmResult.NoError)
            {
                throw new MmException(result, nameof(WaveInterop.NativeMethods.waveOutWrite));
            }

            GC.KeepAlive(this);
        }
    }
}
