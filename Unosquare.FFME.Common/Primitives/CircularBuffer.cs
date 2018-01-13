namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    /// A fixed-size buffer that acts as an infinite length one.
    /// This buffer is backed by unmanaged, very fast memory so ensure you call
    /// the dispose method when you are donde using it.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public sealed class CircularBuffer : IDisposable
    {
        #region Private State Variables

        /// <summary>
        /// The locking object to perform synchronization.
        /// </summary>
        private ReaderWriterLock Locker = new ReaderWriterLock();

        /// <summary>
        /// To detect redundant calls
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// The unbmanaged buffer
        /// </summary>
        private IntPtr Buffer = IntPtr.Zero;

        // Property backing
        private int m_ReadableCount = default(int);
        private TimeSpan m_WriteTag = TimeSpan.MinValue;
        private int m_WriteIndex = default(int);
        private int m_ReadIndex = default(int);
        private int m_Length = default(int);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularBuffer"/> class.
        /// </summary>
        /// <param name="bufferLength">Length of the buffer.</param>
        public CircularBuffer(int bufferLength)
        {
            m_Length = bufferLength;
            Buffer = Marshal.AllocHGlobal(m_Length);
            MediaEngine.Platform.NativeMethods.FillMemory(Buffer, (uint)m_Length, 0);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="CircularBuffer"/> class.
        /// </summary>
        ~CircularBuffer()
        {
            Dispose(false);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the capacity of this buffer.
        /// </summary>
        public int Length
        {
            get
            {
                try
                {
                    Locker.AcquireReaderLock(Timeout.Infinite);
                    return m_Length;
                }
                finally
                {
                    Locker.ReleaseReaderLock();
                }
            }
        }

        /// <summary>
        /// Gets the current, 0-based read index
        /// </summary>
        public int ReadIndex
        {
            get
            {
                try
                {
                    Locker.AcquireReaderLock(Timeout.Infinite);
                    return m_ReadIndex;
                }
                finally
                {
                    Locker.ReleaseReaderLock();
                }
            }
        }

        /// <summary>
        /// Gets the maximum rewindable amount of bytes.
        /// </summary>
        public int RewindableCount
        {
            get
            {
                try
                {
                    Locker.AcquireReaderLock(Timeout.Infinite);
                    if (m_WriteIndex < m_ReadIndex)
                        return m_ReadIndex - m_WriteIndex;

                    return m_ReadIndex;
                }
                finally
                {
                    Locker.ReleaseReaderLock();
                }
            }
        }

        /// <summary>
        /// Gets the current, 0-based write index.
        /// </summary>
        public int WriteIndex
        {
            get
            {
                try
                {
                    Locker.AcquireReaderLock(Timeout.Infinite);
                    return m_WriteIndex;
                }
                finally
                {
                    Locker.ReleaseReaderLock();
                }
            }
        }

        /// <summary>
        /// Gets an the object associated with the last write
        /// </summary>
        public TimeSpan WriteTag
        {
            get
            {
                try
                {
                    Locker.AcquireReaderLock(Timeout.Infinite);
                    return m_WriteTag;
                }
                finally
                {
                    Locker.ReleaseReaderLock();
                }
            }
        }

        /// <summary>
        /// Gets the available bytes to read.
        /// </summary>
        public int ReadableCount
        {
            get
            {
                try
                {
                    Locker.AcquireReaderLock(Timeout.Infinite);
                    return m_ReadableCount;
                }
                finally
                {
                    Locker.ReleaseReaderLock();
                }
            }
        }

        /// <summary>
        /// Gets the number of bytes that can be written.
        /// </summary>
        public int WritableCount
        {
            get
            {
                try
                {
                    Locker.AcquireReaderLock(Timeout.Infinite);
                    return m_Length - m_ReadableCount;
                }
                finally
                {
                    Locker.ReleaseReaderLock();
                }
            }
        }

        /// <summary>
        /// Gets percentage of used bytes (readbale/available, from 0.0 to 1.0).
        /// </summary>
        public double CapacityPercent
        {
            get
            {
                try
                {
                    Locker.AcquireReaderLock(Timeout.Infinite);
                    return 1.0 * m_ReadableCount / m_Length;
                }
                finally
                {
                    Locker.ReleaseReaderLock();
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Skips the specified amount requested bytes to be read.
        /// </summary>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <exception cref="System.InvalidOperationException">When requested bytes GT readable count</exception>
        public void Skip(int requestedBytes)
        {
            try
            {
                Locker.AcquireWriterLock(Timeout.Infinite);
                if (requestedBytes > m_ReadableCount)
                {
                    throw new InvalidOperationException(
                        $"Unable to skip {requestedBytes} bytes. Only {m_ReadableCount} bytes are available for skipping");
                }

                m_ReadIndex += requestedBytes;
                m_ReadableCount -= requestedBytes;

                if (m_ReadIndex >= m_Length)
                    m_ReadIndex = 0;
            }
            finally
            {
                Locker.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// Rewinds the read position by specified requested amount of bytes.
        /// </summary>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <exception cref="InvalidOperationException">When requested GT rewindable</exception>
        public void Rewind(int requestedBytes)
        {
            try
            {
                Locker.AcquireWriterLock(Timeout.Infinite);
                if (requestedBytes > RewindableCount)
                {
                    throw new InvalidOperationException(
                        $"Unable to rewind {requestedBytes} bytes. Only {RewindableCount} bytes are available for rewinding");
                }

                m_ReadIndex -= requestedBytes;
                m_ReadableCount += requestedBytes;

                if (m_ReadIndex < 0)
                    m_ReadIndex = 0;
            }
            finally
            {
                Locker.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// Reads the specified number of bytes into the target array.
        /// </summary>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <param name="target">The target.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <exception cref="System.InvalidOperationException">When requested GT readble</exception>
        public void Read(int requestedBytes, byte[] target, int targetOffset)
        {
            try
            {
                Locker.AcquireWriterLock(Timeout.Infinite);
                if (requestedBytes > m_ReadableCount)
                {
                    throw new InvalidOperationException(
                        $"Unable to read {requestedBytes} bytes. Only {m_ReadableCount} bytes are available");
                }

                var readCount = 0;
                while (readCount < requestedBytes)
                {
                    var copyLength = Math.Min(m_Length - m_ReadIndex, requestedBytes - readCount);
                    var sourcePtr = Buffer + m_ReadIndex;
                    Marshal.Copy(sourcePtr, target, targetOffset + readCount, copyLength);

                    readCount += copyLength;
                    m_ReadIndex += copyLength;
                    m_ReadableCount -= copyLength;

                    if (m_ReadIndex >= m_Length)
                        m_ReadIndex = 0;
                }
            }
            finally
            {
                Locker.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// Writes data to the backing buffer using the specified pointer and length.
        /// and associating a write tag for this operation.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="length">The length.</param>
        /// <param name="writeTag">The write tag.</param>
        /// <param name="overwrite">if set to <c>true</c>, overwrites the data even if it has not been read.</param>
        /// <exception cref="InvalidOperationException">Read</exception>
        /// <exception cref="System.InvalidOperationException">When read needs to be called more!</exception>
        public void Write(IntPtr source, int length, TimeSpan writeTag, bool overwrite)
        {
            try
            {
                Locker.AcquireWriterLock(Timeout.Infinite);
                if (overwrite == false && length > WritableCount)
                {
                    throw new InvalidOperationException(
                        $"Unable to write to circular buffer. Call the {nameof(Read)} method to make some additional room");
                }

                var writeCount = 0;
                while (writeCount < length)
                {
                    var copyLength = Math.Min(m_Length - m_WriteIndex, length - writeCount);
                    var sourcePtr = source + writeCount;
                    var targetPtr = Buffer + m_WriteIndex;
                    MediaEngine.Platform.NativeMethods.CopyMemory(targetPtr, sourcePtr, (uint)copyLength);

                    writeCount += copyLength;
                    m_WriteIndex += copyLength;
                    m_ReadableCount += copyLength;

                    if (m_WriteIndex >= m_Length)
                        m_WriteIndex = 0;
                }

                m_WriteTag = writeTag;
            }
            finally
            {
                Locker.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// Resets all states as if this buffer had just been created.
        /// </summary>
        public void Clear()
        {
            try
            {
                Locker.AcquireWriterLock(Timeout.Infinite);
                m_WriteIndex = 0;
                m_ReadIndex = 0;
                m_WriteTag = TimeSpan.MinValue;
                m_ReadableCount = 0;
            }
            finally
            {
                Locker.ReleaseWriterLock();
            }
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            try
            {
                Locker.AcquireWriterLock(Timeout.Infinite);
                if (IsDisposed) return;

                if (alsoManaged)
                    Clear();

                Marshal.FreeHGlobal(Buffer);
                Buffer = IntPtr.Zero;
                m_Length = 0;

                IsDisposed = true;
            }
            finally
            {
                Locker.ReleaseWriterLock();
            }
        }

        #endregion
    }
}
