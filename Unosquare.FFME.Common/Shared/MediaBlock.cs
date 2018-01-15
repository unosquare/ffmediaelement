namespace Unosquare.FFME.Shared
{
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
        public TimeSpan SnapTime
        {
            get
            {
                if (Duration.Ticks <= 0) return StartTime;
                return TimeSpan.FromTicks(StartTime.Ticks + (TimeSpan.TicksPerMillisecond / 10));
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
        public int CompareTo(MediaBlock other)
        {
            return StartTime.CompareTo(other.StartTime);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public abstract void Dispose();
    }
}
