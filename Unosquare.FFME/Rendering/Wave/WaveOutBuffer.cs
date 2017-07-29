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
        private readonly Int32 bufferSize; // allocated bytes, may not be the same as bytes read
        private readonly byte[] buffer;
        private readonly IWaveProvider waveStream;
        private readonly object waveOutLock;
        private GCHandle bufferHandle;
        private IntPtr waveOutPtr;
        private GCHandle headerHandle; // we need to pin the header structure
        private GCHandle callbackHandle; // for the user callback

        /// <summary>
        /// Initializes a new instance of the <see cref="WaveOutBuffer"/> class.
        /// </summary>
        /// <param name="hWaveOut">WaveOut device to write to</param>
        /// <param name="bufferSize">Buffer size in bytes</param>
        /// <param name="bufferFillStream">Stream to provide more data</param>
        /// <param name="waveOutLock">Lock to protect WaveOut API's from being called on &gt;1 thread</param>
        public WaveOutBuffer(IntPtr hWaveOut, Int32 bufferSize, IWaveProvider bufferFillStream, object waveOutLock)
        {
            this.bufferSize = bufferSize;
            buffer = new byte[bufferSize];
            bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            this.waveOutPtr = hWaveOut;
            waveStream = bufferFillStream;
            this.waveOutLock = waveOutLock;

            header = new WaveHeader();
            headerHandle = GCHandle.Alloc(header, GCHandleType.Pinned);
            header.DataBuffer = bufferHandle.AddrOfPinnedObject();
            header.BufferLength = bufferSize;
            header.Loops = 1;
            callbackHandle = GCHandle.Alloc(this);
            header.UserData = (IntPtr)callbackHandle;
            lock (waveOutLock)
            {
                MmException.Try(WaveInterop.NativeMethods.waveOutPrepareHeader(hWaveOut, header, Marshal.SizeOf(header)), "waveOutPrepareHeader");
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="WaveOutBuffer"/> class.
        /// </summary>
        ~WaveOutBuffer()
        {
            Dispose(false);
            System.Diagnostics.Debug.Assert(true, "WaveBuffer was not disposed");
        }

        /// <summary>
        /// Whether the header's in queue flag is set
        /// </summary>
        public bool InQueue
        {
            get
            {
                return (header.Flags & WaveHeaderFlags.InQueue) == WaveHeaderFlags.InQueue;
            }
        }

        /// <summary>
        /// The buffer size in bytes
        /// </summary>
        public int BufferSize => bufferSize;

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
        /// This calls the .Read method on the stream
        /// </summary>
        /// <returns>true when bytes were written. False if no bytes were written.</returns>
        internal bool OnDone()
        {
            int bytes;
            lock (waveStream)
            {
                bytes = waveStream.Read(buffer, 0, buffer.Length);
            }

            if (bytes == 0)
                return false;

            for (int n = bytes; n < buffer.Length; n++)
                buffer[n] = 0;

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

            if (headerHandle.IsAllocated)
                headerHandle.Free();
            if (bufferHandle.IsAllocated)
                bufferHandle.Free();
            if (callbackHandle.IsAllocated)
                callbackHandle.Free();
            if (waveOutPtr != IntPtr.Zero)
            {
                lock (waveOutLock)
                    WaveInterop.NativeMethods.waveOutUnprepareHeader(waveOutPtr, header, Marshal.SizeOf(header));

                waveOutPtr = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Writes to wave out.
        /// </summary>
        /// <exception cref="Unosquare.FFME.Rendering.Wave.MmException">waveOutWrite</exception>
        private void WriteToWaveOut()
        {
            MmResult result;

            lock (waveOutLock)
                result = WaveInterop.NativeMethods.waveOutWrite(waveOutPtr, header, Marshal.SizeOf(header));

            if (result != MmResult.NoError)
            {
                throw new MmException(result, nameof(WaveInterop.NativeMethods.waveOutWrite));
            }

            GC.KeepAlive(this);
        }
    }
}
