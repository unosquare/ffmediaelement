namespace Unosquare.FFME.Shared
{
    using Decoding;
    using FFmpeg.AutoGen;
    using System;

    /// <summary>
    /// Represents a seek entry to a position within the stream
    /// </summary>
    public sealed class VideoSeekIndexEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VideoSeekIndexEntry" /> class.
        /// </summary>
        /// <param name="streamIndex">Index of the stream.</param>
        /// <param name="timeBaseNum">The time base numerator.</param>
        /// <param name="timeBaseDen">The time base deonominator.</param>
        /// <param name="startTimeTicks">The start time ticks.</param>
        /// <param name="presentationTime">The presentation time.</param>
        /// <param name="decodingTime">The decoding time.</param>
        internal VideoSeekIndexEntry(int streamIndex, int timeBaseNum, int timeBaseDen, long startTimeTicks, long presentationTime, long decodingTime)
        {
            StreamIndex = streamIndex;
            StartTime = TimeSpan.FromTicks(streamIndex);
            PresentationTime = presentationTime;
            DecodingTime = decodingTime;
            StreamTimeBase = new AVRational { num = timeBaseNum, den = timeBaseDen };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoSeekIndexEntry"/> class.
        /// </summary>
        /// <param name="frame">The frame.</param>
        internal VideoSeekIndexEntry(VideoFrame frame)
        {
            StreamIndex = frame.StreamIndex;
            StreamTimeBase = frame.StreamTimeBase;
            StartTime = frame.StartTime;
            PresentationTime = frame.PresentationTime;
            DecodingTime = frame.DecodingTime;
        }

        /// <summary>
        /// Gets the stream index of this index entry.
        /// </summary>
        public int StreamIndex { get; }

        /// <summary>
        /// Gets the stream time base.
        /// </summary>
        public AVRational StreamTimeBase { get; }

        /// <summary>
        /// Gets the start time of the frame.
        /// </summary>
        public TimeSpan StartTime { get; }

        /// <summary>
        /// Gets the original, unadjusted presentation time.
        /// </summary>
        public long PresentationTime { get; }

        /// <summary>
        /// Gets the original, unadjusted decoding time.
        /// </summary>
        public long DecodingTime { get; }

        /// <inheritdoc />
        public override string ToString() =>
            $"{StreamIndex},{StreamTimeBase.num},{StreamTimeBase.den},{StartTime.Ticks},{PresentationTime},{DecodingTime}";
    }
}
