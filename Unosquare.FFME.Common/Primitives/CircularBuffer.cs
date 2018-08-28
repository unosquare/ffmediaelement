namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A fixed-size buffer that acts as an infinite length one.
    /// This buffer is backed by unmanaged, very fast memory so ensure you call
    /// the dispose method when you are done using it.
    /// </summary>
    /// <seealso cref="IDisposable" />
    public sealed class CircularBuffer : IDisposable
    {
        #region Private State Variables

        /// <summary>
        /// The locking object to perform synchronization.
        /// </summary>
        private readonly object SyncLock = new object();

        /// <summary>
        /// The unmanaged buffer
        /// </summary>
        private IntPtr Buffer;

        // Property backing
        private bool m_IsDisposed;
        private int m_ReadableCount;
        private TimeSpan m_WriteTag = TimeSpan.MinValue;
        private int m_WriteIndex;
        private int m_ReadIndex;
        private int m_Length;

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

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        public bool IsDisposed { get { lock (SyncLock) return m_IsDisposed; } }

        /// <summary>
        /// Gets the capacity of this buffer.
        /// </summary>
        public int Length { get { lock (SyncLock) return m_Length; } }

        /// <summary>
        /// Gets the current, 0-based read index
        /// </summary>
        public int ReadIndex { get { lock (SyncLock) return m_ReadIndex; } }

        /// <summary>
        /// Gets the maximum rewindable amount of bytes.
        /// </summary>
        public int RewindableCount
        {
            get
            {
                lock (SyncLock)
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
        public int WriteIndex { get { lock (SyncLock) return m_WriteIndex; } }

        /// <summary>
        /// Gets an the object associated with the last write
        /// </summary>
        public TimeSpan WriteTag { get { lock (SyncLock) return m_WriteTag; } }

        /// <summary>
        /// Gets the available bytes to read.
        /// </summary>
        public int ReadableCount { get { lock (SyncLock) return m_ReadableCount; } }

        /// <summary>
        /// Gets the number of bytes that can be written.
        /// </summary>
        public int WritableCount { get { lock (SyncLock) return m_Length - m_ReadableCount; } }

        /// <summary>
        /// Gets percentage of used bytes (readbale/available, from 0.0 to 1.0).
        /// </summary>
        public double CapacityPercent { get { lock (SyncLock) return (double)m_ReadableCount / m_Length; } }

        #endregion

        #region Methods

        /// <summary>
        /// Skips the specified amount requested bytes to be read.
        /// </summary>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <exception cref="InvalidOperationException">When requested bytes GT readable count</exception>
        public void Skip(int requestedBytes)
        {
            lock (SyncLock)
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
            lock (SyncLock)
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
            lock (SyncLock)
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
            lock (SyncLock)
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
            lock (SyncLock)
            {
                m_WriteIndex = 0;
                m_ReadIndex = 0;
                m_WriteTag = TimeSpan.MinValue;
                m_ReadableCount = 0;
            }
        }

        #endregion

        #region IDisposable Support

        /// <inheritdoc />
        public void Dispose()
        {
            lock (SyncLock)
            {
                if (m_IsDisposed == true) return;

                Clear();
                Marshal.FreeHGlobal(Buffer);
                Buffer = IntPtr.Zero;
                m_Length = 0;
                m_IsDisposed = true;
            }
        }

        #endregion
    }
}
