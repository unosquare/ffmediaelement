namespace Unosquare.FFME.Events
{
    using Shared;
    using System;

    /// <summary>
    /// A base class to represent media block
    /// rendering event arguments.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public abstract class RenderingEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingEventArgs" /> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="startTime">The position.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="clock">The clock.</param>
        protected RenderingEventArgs(StreamInfo stream, TimeSpan startTime, TimeSpan duration, TimeSpan clock)
        {
            StartTime = startTime;
            Duration = duration;
            Clock = clock;
            Stream = stream;
        }

        /// <summary>
        /// Provides Stream Information coming from the media container.
        /// </summary>
        public StreamInfo Stream { get; }

        /// <summary>
        /// Gets the clock position at which the media
        /// was called for rendering
        /// </summary>
        public TimeSpan Clock { get; }

        /// <summary>
        /// Gets the starting time at which this media
        /// has to be presented.
        /// </summary>
        public TimeSpan StartTime { get; }

        /// <summary>
        /// Gets how long this media has to be presented.
        /// </summary>
        public TimeSpan Duration { get; }
    }
}
