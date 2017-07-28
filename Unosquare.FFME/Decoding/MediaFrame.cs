namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using System;

    /// <summary>
    /// Represents a wrapper for an unmanaged frame.
    /// Derived classes implement the specifics of each media type.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    internal unsafe abstract class MediaFrame : IDisposable, IComparable<MediaFrame>
    {
        #region Private Members

        protected void* InternalPointer;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaFrame" /> class.
        /// </summary>
        /// <param name="pointer">The pointer.</param>
        /// <param name="component">The component.</param>
        internal MediaFrame(void* pointer, MediaComponent component)
        {
            InternalPointer = pointer;
            StreamTimeBase = component.Stream->time_base;
            StreamIndex = component.StreamIndex;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the type of the media.
        /// </summary>
        /// <value>
        /// The type of the media.
        /// </value>
        public abstract MediaType MediaType { get; }

        /// <summary>
        /// Gets the start time of the frame.
        /// </summary>
        public TimeSpan StartTime { get; protected set; }

        /// <summary>
        /// Gets the end time of the frame
        /// </summary>
        public TimeSpan EndTime { get; protected set; }

        /// <summary>
        /// Gets the index of the stream from which this frame was decoded.
        /// </summary>
        public int StreamIndex { get; protected set; }

        /// <summary>
        /// Gets the time base of the stream that generated this frame.
        /// </summary>
        internal AVRational StreamTimeBase { get; }

        /// <summary>
        /// Gets the amount of time this data has to be presented
        /// </summary>
        public TimeSpan Duration { get; protected set; }

        /// <summary>
        /// When the unmanaged frame is released (freed from unmanaged memory)
        /// this property will return true.
        /// </summary>
        public bool IsStale { get { return InternalPointer == null; } }

        #endregion

        #region Methods

        /// <summary>
        /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
        /// </summary>
        /// <param name="other">An object to compare with this instance.</param>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared. The return value has these meanings: Value Meaning Less than zero This instance precedes <paramref name="other" /> in the sort order.  Zero This instance occurs in the same position in the sort order as <paramref name="other" />. Greater than zero This instance follows <paramref name="other" /> in the sort order.
        /// </returns>
        public int CompareTo(MediaFrame other)
        {
            return StartTime.CompareTo(other.StartTime);
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public abstract void Dispose();

        #endregion
    }

}
