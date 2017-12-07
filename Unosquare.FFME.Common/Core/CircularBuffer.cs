namespace Unosquare.FFME.Core
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A fixed-size buffer that acts as an infinite length one.
    /// This buffer is backed by unmanaged, very fast memory so ensure you call
    /// the dispose method when you are donde using it.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    internal sealed class CircularBuffer : IDisposable
    {
        #region Private State Variables

        /// <summary>
        /// The locking object to perform synchronization.
        /// </summary>
        private readonly object SyncLock = new object();

        /// <summary>
        /// To detect redundant calls
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// The unbmanaged buffer
        /// </summary>
        private IntPtr Buffer = IntPtr.Zero;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularBuffer"/> class.
        /// </summary>
        /// <param name="bufferLength">Length of the buffer.</param>
        public CircularBuffer(int bufferLength)
        {
            Length = bufferLength;
            Buffer = Marshal.AllocHGlobal(Length);
            Platform.FillMemory(Buffer, (uint)Length, 0);
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
        public int Length { get; private set; }

        /// <summary>
        /// Gets the current, 0-based read index
        /// </summary>
        public int ReadIndex { get; private set; }

        /// <summary>
        /// Gets the maximum rewindable amount of bytes.
        /// </summary>
        public int RewindableCount
        {
            get
            {
                lock (SyncLock)
                {
                    if (WriteIndex < ReadIndex)
                        return ReadIndex - WriteIndex;

                    return ReadIndex;
                }
            }
        }

        /// <summary>
        /// Gets the current, 0-based write index.
        /// </summary>
        public int WriteIndex { get; private set; }

        /// <summary>
        /// Gets an the object associated with the last write
        /// </summary>
        public TimeSpan WriteTag { get; private set; } = TimeSpan.MinValue;

        /// <summary>
        /// Gets the available bytes to read.
        /// </summary>
        public int ReadableCount { get; private set; }

        /// <summary>
        /// Gets the number of bytes that can be written.
        /// </summary>
        public int WritableCount
        {
            get { return Length - ReadableCount; }
        }

        /// <summary>
        /// Gets percentage of used bytes (readbale/available, from 0.0 to 1.0).
        /// </summary>
        public double CapacityPercent
        {
            get { return 1.0 * ReadableCount / Length; }
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
            lock (SyncLock)
            {
                if (requestedBytes > ReadableCount)
                {
                    throw new InvalidOperationException(
                        $"Unable to skip {requestedBytes} bytes. Only {ReadableCount} bytes are available for skipping");
                }

                ReadIndex += requestedBytes;
                ReadableCount -= requestedBytes;

                if (ReadIndex >= Length)
                    ReadIndex = 0;
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

                ReadIndex -= requestedBytes;
                ReadableCount += requestedBytes;

                if (ReadIndex < 0)
                    ReadIndex = 0;
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
            lock (SyncLock)
            {
                if (requestedBytes > ReadableCount)
                {
                    throw new InvalidOperationException(
                        $"Unable to read {requestedBytes} bytes. Only {ReadableCount} bytes are available");
                }

                var readCount = 0;
                while (readCount < requestedBytes)
                {
                    var copyLength = Math.Min(Length - ReadIndex, requestedBytes - readCount);
                    var sourcePtr = Buffer + ReadIndex;
                    Marshal.Copy(sourcePtr, target, targetOffset + readCount, copyLength);

                    readCount += copyLength;
                    ReadIndex += copyLength;
                    ReadableCount -= copyLength;

                    if (ReadIndex >= Length)
                        ReadIndex = 0;
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
        /// <exception cref="InvalidOperationException">Read</exception>
        /// <exception cref="System.InvalidOperationException">When read needs to be called more!</exception>
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
                    var copyLength = Math.Min(Length - WriteIndex, length - writeCount);
                    var sourcePtr = source + writeCount;
                    var targetPtr = Buffer + WriteIndex;
                    Platform.CopyMemory(targetPtr, sourcePtr, (uint)copyLength);

                    writeCount += copyLength;
                    WriteIndex += copyLength;
                    ReadableCount += copyLength;

                    if (WriteIndex >= Length)
                        WriteIndex = 0;
                }

                WriteTag = writeTag;
            }
        }

        /// <summary>
        /// Resets all states as if this buffer had just been created.
        /// </summary>
        public void Clear()
        {
            lock (SyncLock)
            {
                WriteIndex = 0;
                ReadIndex = 0;
                WriteTag = TimeSpan.MinValue;
                ReadableCount = 0;
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
            lock (SyncLock)
            {
                if (IsDisposed) return;

                if (alsoManaged)
                    Clear();

                Marshal.FreeHGlobal(Buffer);
                Buffer = IntPtr.Zero;
                Length = 0;

                IsDisposed = true;
            }
        }

        #endregion
    }
}
