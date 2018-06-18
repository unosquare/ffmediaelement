namespace Unosquare.FFME.Shared
{
    using FFmpeg.AutoGen;
    using Primitives;
    using System;

    /// <summary>
    /// A base class for blocks of the deifferent MediaTypes.
    /// Blocks are the result of decoding and scaling a frame.
    /// Blocks have preallocated buffers wich makes them memory and CPU efficient.
    /// Reuse blocks as much as possible. Once you create a block from a frame,
    /// you don't need the frame anymore so make sure you dispose the frame.
    /// </summary>
    public abstract class MediaBlock : IComparable<MediaBlock>, IDisposable
    {
        private readonly object SyncLock = new object();
        private bool m_IsDisposed = false;
        private ISyncLocker Locker = SyncLockerFactory.Create(useSlim: true);
        private IntPtr m_Buffer = IntPtr.Zero;
        private int m_BufferLength = default;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaBlock"/> class.
        /// </summary>
        protected MediaBlock()
        {
            // placeholder
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="MediaBlock"/> class.
        /// </summary>
        ~MediaBlock() => Dispose(false);

        /// <summary>
        /// Gets the media type of the data
        /// </summary>
        public abstract MediaType MediaType { get; }

        /// <summary>
        /// Gets a value indicating whether the start time was guessed from siblings
        /// or the source frame PTS comes from a NO PTS value
        /// </summary>
        public bool IsStartTimeGuessed { get; internal set; }

        /// <summary>
        /// Gets the time at which this data should be presented (PTS)
        /// </summary>
        public TimeSpan StartTime { get; internal set; }

        /// <summary>
        /// Gets the amount of time this data has to be presented
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
        /// Gets a safe timestamp the the block can be displayed.
        /// Returns StartTime if the duration is Zero or negative.
        /// </summary>
        public TimeSpan SnapTime => (Duration.Ticks <= 0) ?
            StartTime : TimeSpan.FromTicks(StartTime.Ticks + TimeSpan.TicksPerMillisecond);

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
                {
                    return m_IsDisposed == false && m_Buffer != IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether this block is disposed
        /// </summary>
        public bool IsDisposed
        {
            get { lock (SyncLock) return m_IsDisposed; }
        }

        /// <summary>
        /// Tries the acquire a reader lock on the unmanaged buffer.
        /// Returns false if the buffer has been disposed.
        /// </summary>
        /// <param name="locker">The locker.</param>
        /// <returns>The disposable lock</returns>
        public bool TryAcquireReaderLock(out IDisposable locker)
        {
            locker = null;
            lock (SyncLock)
            {
                if (m_IsDisposed) return false;
                return Locker.TryAcquireReaderLock(out locker);
            }
        }

        /// <summary>
        /// Tries the acquire a writer lock on the unmanaged buffer.
        /// Returns false if the buffer has been disposed or a lock operation times out.
        /// </summary>
        /// <param name="locker">The locker.</param>
        /// <returns>The disposable lock</returns>
        public bool TryAcquireWriterLock(out IDisposable locker)
        {
            locker = null;
            lock (SyncLock)
            {
                if (m_IsDisposed) return false;
                return Locker.TryAcquireWriterLock(out locker);
            }
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
            if (Duration <= TimeSpan.Zero)
                return false;

            return position.Ticks >= StartTime.Ticks
                && position.Ticks <= EndTime.Ticks;
        }

        /// <summary>
        /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
        /// </summary>
        /// <param name="other">An object to compare with this instance.</param>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared. The return value has these meanings: Value Meaning Less than zero This instance precedes <paramref name="other" /> in the sort order.  Zero This instance occurs in the same position in the sort order as <paramref name="other" />. Greater than zero This instance follows <paramref name="other" /> in the sort order.
        /// </returns>
        public int CompareTo(MediaBlock other) => StartTime.CompareTo(other.StartTime);

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Allocates the specified buffer length.
        /// </summary>
        /// <param name="bufferLength">Length of the buffer.</param>
        /// <returns>True if the buffer is successfully allocated</returns>
        internal virtual unsafe bool Allocate(int bufferLength)
        {
            if (bufferLength <= 0)
                throw new ArgumentException($"{nameof(bufferLength)} must be greater than 0");

            lock (SyncLock)
            {
                if (m_IsDisposed) return false;

                if (m_BufferLength == bufferLength)
                    return true;

                if (Locker.TryAcquireWriterLock(out var writeLock))
                {
                    using (writeLock)
                    {
                        m_Buffer = new IntPtr(ffmpeg.av_malloc((ulong)bufferLength));
                        m_BufferLength = bufferLength;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool alsoManaged)
        {
            lock (SyncLock)
            {
                if (m_IsDisposed) return;

                if (alsoManaged)
                {
                    // Dispose managed state (managed objects).
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                using (Locker.AcquireWriterLock())
                {
                    Deallocate();
                }

                // set large fields to null.
                Locker.Dispose();
                Locker = null;
                m_IsDisposed = true;
            }
        }

        /// <summary>
        /// Deallocates the picture buffer and resets the related buffer properties
        /// </summary>
        protected virtual unsafe void Deallocate()
        {
            if (m_Buffer == IntPtr.Zero) return;

            ffmpeg.av_free(m_Buffer.ToPointer());
            m_Buffer = IntPtr.Zero;
            m_BufferLength = default;
        }
    }
}
