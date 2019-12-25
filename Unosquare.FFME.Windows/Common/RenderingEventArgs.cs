namespace Unosquare.FFME.Common
{
    using System;

    /// <summary>
    /// A base class to represent media block
    /// rendering event arguments.
    /// </summary>
    /// <seealso cref="EventArgs" />
    public abstract class RenderingEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingEventArgs" /> class.
        /// </summary>
        /// <param name="engineState">The media engine state.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="startTime">The position.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="clock">The clock.</param>
        /// <param name="pts">The original unadjusted PTS of the frame.</param>
        protected RenderingEventArgs(IMediaEngineState engineState, StreamInfo stream, TimeSpan startTime, TimeSpan duration, TimeSpan clock, long pts)
        {
            EngineState = engineState;
            StartTime = startTime;
            Duration = duration;
            Clock = clock;
            Stream = stream;
            PresentationTime = pts;
        }

        /// <summary>
        /// Provides access to the underlying media engine state.
        /// </summary>
        public IMediaEngineState EngineState { get; }

        /// <summary>
        /// Provides Stream Information coming from the media container.
        /// </summary>
        public StreamInfo Stream { get; }

        /// <summary>
        /// Gets the clock position at which the media
        /// was called for rendering.
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

        /// <summary>
        /// Gets the unadjusted, original presentation timestamp (PTS) of the frame in
        /// <see cref="StreamInfo.TimeBase"/> units. May return <see cref="FFmpeg.AutoGen.ffmpeg.AV_NOPTS_VALUE"/>
        /// when invialid, not applicable (as in continuous audio rendering), or unavailable.
        /// </summary>
        public long PresentationTime { get; }
    }
}
