namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A fixed-size buffer that acts as an infinite length one.
    /// This buffer is backed by unmanaged, very fast memory so ensure you call
    /// the dispose method when you are donde using it.
    /// </summary>
    /// <seealso cref="IDisposable" />
    public sealed class CircularBuffer : IDisposable
    {
        #region Private State Variables

        /// <summary>
        /// The locking object to perform synchronization.
        /// </summary>
        private ISyncLocker Locker = SyncLockerFactory.Create(useSlim: true);

        /// <summary>
        /// To detect redundant calls
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// The unmanaged buffer
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
            MediaEngine.Platform.NativeMethods.FillMemory(Buffer, Convert.ToUInt32(m_Length), 0);
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
                using (Locker.AcquireReaderLock())
                {
                    return m_Length;
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
                using (Locker.AcquireReaderLock())
                {
                    return m_ReadIndex;
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
                using (Locker.AcquireReaderLock())
                {
                    if (m_WriteIndex < m_ReadIndex)
                        return m_ReadIndex - m_WriteIndex;

                    return m_ReadIndex;
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
                using (Locker.AcquireReaderLock())
                {
                    return m_WriteIndex;
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
                using (Locker.AcquireReaderLock())
                {
                    return m_WriteTag;
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
                using (Locker.AcquireReaderLock())
                {
                    return m_ReadableCount;
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
                using (Locker.AcquireReaderLock())
                {
                    return m_Length - m_ReadableCount;
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
                using (Locker.AcquireReaderLock())
                {
                    return 1.0 * m_ReadableCount / m_Length;
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Skips the specified amount requested bytes to be read.
        /// </summary>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <exception cref="InvalidOperationException">When requested bytes GT readable count</exception>
        public void Skip(int requestedBytes)
        {
            using (Locker.AcquireWriterLock())
            {
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
        }

        /// <summary>
        /// Rewinds the read position by specified requested amount of bytes.
        /// </summary>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <exception cref="InvalidOperationException">When requested GT rewindable</exception>
        public void Rewind(int requestedBytes)
        {
            using (Locker.AcquireWriterLock())
            {
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
        }

        /// <summary>
        /// Reads the specified number of bytes into the target array.
        /// </summary>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <param name="target">The target.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <exception cref="InvalidOperationException">When requested bytes is greater than readble count</exception>
        public void Read(int requestedBytes, byte[] target, int targetOffset)
        {
            using (Locker.AcquireWriterLock())
            {
                if (requestedBytes > m_ReadableCount)
                {
                    throw new InvalidOperationException(
                        $"Unable to read {requestedBytes} bytes. Only {m_ReadableCount} bytes are available.");
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
        }

        /// <summary>
        /// Writes data to the backing buffer using the specified pointer and length.
        /// and associating a write tag for this operation.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="length">The length.</param>
        /// <param name="writeTag">The write tag.</param>
        /// <param name="overwrite">if set to <c>true</c>, overwrites the data even if it has not been read.</param>
        /// <exception cref="InvalidOperationException">When read needs to be called more often!</exception>
        public void Write(IntPtr source, int length, TimeSpan writeTag, bool overwrite)
        {
            using (Locker.AcquireWriterLock())
            {
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
                    MediaEngine.Platform.NativeMethods.CopyMemory(targetPtr, sourcePtr, Convert.ToUInt32(copyLength));

                    writeCount += copyLength;
                    m_WriteIndex += copyLength;
                    m_ReadableCount += copyLength;

                    if (m_WriteIndex >= m_Length)
                        m_WriteIndex = 0;
                }

                m_WriteTag = writeTag;
            }
        }

        /// <summary>
        /// Resets all states as if this buffer had just been created.
        /// </summary>
        public void Clear()
        {
            using (Locker.AcquireWriterLock())
            {
                m_WriteIndex = 0;
                m_ReadIndex = 0;
                m_WriteTag = TimeSpan.MinValue;
                m_ReadableCount = 0;
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
            if (IsDisposed) return;

            if (alsoManaged)
            {
                Clear();
                Locker?.Dispose();
            }

            Marshal.FreeHGlobal(Buffer);
            Buffer = IntPtr.Zero;
            m_Length = 0;
            Locker = null;

            IsDisposed = true;
        }

        #endregion
    }
}
