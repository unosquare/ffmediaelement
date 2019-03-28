namespace Unosquare.FFME.Container
{
    using FFmpeg.AutoGen;
    using Media;
    using Primitives;
    using System;

    /// <summary>
    /// A base class for blocks of the different MediaTypes.
    /// Blocks are the result of decoding and scaling a frame.
    /// Blocks have pre-allocated buffers which makes them memory and CPU efficient.
    /// Reuse blocks as much as possible. Once you create a block from a frame,
    /// you don't need the frame anymore so make sure you dispose the frame.
    /// </summary>
    internal abstract class MediaBlock
        : IComparable<MediaBlock>, IComparable<TimeSpan>, IComparable<long>, IEquatable<MediaBlock>, IDisposable
    {
        private readonly object SyncLock = new object();
        private readonly ISyncLocker Locker = SyncLockerFactory.Create(useSlim: true);
        private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);
        private IntPtr m_Buffer = IntPtr.Zero;
        private int m_BufferLength;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaBlock" /> class.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        protected MediaBlock(MediaType mediaType)
        {
            MediaType = mediaType;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="MediaBlock"/> class.
        /// </summary>
        ~MediaBlock() => Dispose(false);

        /// <summary>
        /// Gets the media type of the data.
        /// </summary>
        public MediaType MediaType { get; }

        /// <summary>
        /// Gets the size of the compressed frame.
        /// </summary>
        public int CompressedSize { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the start time was guessed from siblings
        /// or the source frame PTS comes from a NO PTS value.
        /// </summary>
        public bool IsStartTimeGuessed { get; internal set; }

        /// <summary>
        /// Gets the time at which this data should be presented (PTS).
        /// </summary>
        public TimeSpan StartTime { get; internal set; }

        /// <summary>
        /// Gets the amount of time this data has to be presented.
        /// </summary>
        public TimeSpan Duration { get; internal set; }

        /// <summary>
        /// Gets the end time.
        /// </summary>
        public TimeSpan EndTime { get; internal set; }

        /// <summary>
        /// Gets the index of the stream.
        /// </summary>
        public int StreamIndex { get; internal set; }

        /// <summary>
        /// Gets a pointer to the first byte of the unmanaged data buffer.
        /// </summary>
        public IntPtr Buffer { get { lock (SyncLock) return m_Buffer; } }

        /// <summary>
        /// Gets the length of the unmanaged buffer in bytes.
        /// </summary>
        public int BufferLength { get { lock (SyncLock) return m_BufferLength; } }

        /// <summary>
        /// Gets a value indicating whether an unmanaged buffer has been allocated.
        /// </summary>
        public bool IsAllocated
        {
            get
            {
                lock (SyncLock)
                    return !IsDisposed && m_Buffer != IntPtr.Zero;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this block is disposed.
        /// </summary>
        public bool IsDisposed
        {
            get => m_IsDisposed.Value;
            private set => m_IsDisposed.Value = value;
        }

        /// <summary>
        /// Gets or sets the index within the block buffer.
        /// </summary>
        internal int Index { get; set; }

        /// <summary>
        /// Gets or sets the next MediaBlock.
        /// </summary>
        internal MediaBlock Next { get; set; }

        /// <summary>
        /// Gets or sets the previous MediaBlock.
        /// </summary>
        internal MediaBlock Previous { get; set; }

        #region Operators

        /// <summary>
        /// Implements the operator.
        /// </summary>
        /// <param name="left">The left-hand side operand.</param>
        /// <param name="right">The right-hand side operand.</param>
        /// <returns>The result of the operation.</returns>
        public static bool operator ==(MediaBlock left, MediaBlock right)
        {
            if (left is null)
                return right is null;

            return left.Equals(right);
        }

        /// <summary>
        /// Implements the operator.
        /// </summary>
        /// <param name="left">The left-hand side operand.</param>
        /// <param name="right">The right-hand side operand.</param>
        /// <returns>The result of the operation.</returns>
        public static bool operator !=(MediaBlock left, MediaBlock right) =>
            !(left == right);

        /// <summary>
        /// Implements the operator.
        /// </summary>
        /// <param name="left">The left-hand side operand.</param>
        /// <param name="right">The right-hand side operand.</param>
        /// <returns>The result of the operation.</returns>
        public static bool operator <(MediaBlock left, MediaBlock right) =>
            left == null ? right != null : left.CompareTo(right) < 0;

        /// <summary>
        /// Implements the operator.
        /// </summary>
        /// <param name="left">The left-hand side operand.</param>
        /// <param name="right">The right-hand side operand.</param>
        /// <returns>The result of the operation.</returns>
        public static bool operator <=(MediaBlock left, MediaBlock right) =>
            left == null || left.CompareTo(right) <= 0;

        /// <summary>
        /// Implements the operator.
        /// </summary>
        /// <param name="left">The left-hand side operand.</param>
        /// <param name="right">The right-hand side operand.</param>
        /// <returns>The result of the operation.</returns>
        public static bool operator >(MediaBlock left, MediaBlock right) =>
            left != null && left.CompareTo(right) > 0;

        /// <summary>
        /// Implements the operator.
        /// </summary>
        /// <param name="left">The left-hand side operand.</param>
        /// <param name="right">The right-hand side operand.</param>
        /// <returns>The result of the operation.</returns>
        public static bool operator >=(MediaBlock left, MediaBlock right) =>
            left == null ? right == null : left.CompareTo(right) >= 0;

        #endregion

        /// <summary>
        /// Tries the acquire a reader lock on the unmanaged buffer.
        /// Returns false if the buffer has been disposed.
        /// </summary>
        /// <param name="locker">The locker.</param>
        /// <returns>The disposable lock.</returns>
        public bool TryAcquireReaderLock(out IDisposable locker)
        {
            locker = null;
            lock (SyncLock)
                return !IsDisposed && Locker.TryAcquireReaderLock(out locker);
        }

        /// <summary>
        /// Tries the acquire a writer lock on the unmanaged buffer.
        /// Returns false if the buffer has been disposed or a lock operation times out.
        /// </summary>
        /// <param name="locker">The locker.</param>
        /// <returns>The disposable lock.</returns>
        public bool TryAcquireWriterLock(out IDisposable locker)
        {
            locker = null;
            lock (SyncLock)
                return !IsDisposed && Locker.TryAcquireWriterLock(out locker);
        }

        /// <summary>
        /// Determines whether this media block holds the specified position.
        /// Returns false if it does not have a valid duration.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>
        ///   <c>true</c> if [contains] [the specified position]; otherwise, <c>false</c>.
        /// </returns>
        public bool Contains(TimeSpan position)
        {
            if (!IsDisposed && Duration <= TimeSpan.Zero)
                return false;

            return position.Ticks >= StartTime.Ticks
                && position.Ticks <= EndTime.Ticks;
        }

        /// <inheritdoc />
        public int CompareTo(MediaBlock other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            return StartTime.Ticks.CompareTo(other.StartTime.Ticks);
        }

        /// <inheritdoc />
        public int CompareTo(TimeSpan other)
        {
            return StartTime.Ticks.CompareTo(other.Ticks);
        }

        /// <inheritdoc />
        public int CompareTo(long other)
        {
            return StartTime.Ticks.CompareTo(other);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is MediaBlock other)
                return ReferenceEquals(this, other);

            return false;
        }

        /// <inheritdoc />
        public bool Equals(MediaBlock other) =>
            ReferenceEquals(this, other);

        /// <inheritdoc />
        public override int GetHashCode() =>
            StartTime.Ticks.GetHashCode() ^
            MediaType.GetHashCode();

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Allocates the specified buffer length.
        /// </summary>
        /// <param name="bufferLength">Length of the buffer.</param>
        /// <returns>True if the buffer is successfully allocated.</returns>
        internal virtual unsafe bool Allocate(int bufferLength)
        {
            if (bufferLength <= 0)
                throw new ArgumentException($"{nameof(bufferLength)} must be greater than 0");

            lock (SyncLock)
            {
                if (IsDisposed)
                    return false;

                if (m_BufferLength == bufferLength)
                    return true;

                if (!Locker.TryAcquireWriterLock(out var writeLock))
                    return false;

                using (writeLock)
                {
                    m_Buffer = (IntPtr)ffmpeg.av_malloc((ulong)bufferLength);
                    m_BufferLength = bufferLength;
                    return true;
                }
            }
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool alsoManaged)
        {
            lock (SyncLock)
            {
                if (IsDisposed) return;
                IsDisposed = true;

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                using (Locker.AcquireWriterLock())
                    Deallocate();

                // set large fields to null.
                if (alsoManaged)
                    Locker.Dispose();
            }
        }

        /// <summary>
        /// De-allocates the picture buffer and resets the related buffer properties.
        /// </summary>
        protected virtual unsafe void Deallocate()
        {
            if (m_Buffer == IntPtr.Zero) return;

            ffmpeg.av_free((void*)m_Buffer);
            m_Buffer = IntPtr.Zero;
            m_BufferLength = default;
        }
    }
}
