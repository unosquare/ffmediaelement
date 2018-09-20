namespace Unosquare.FFME.Decoding
{
    using FFmpeg.AutoGen;
    using System;

    /// <summary>
    /// Represents a seek entry to a position within the stream
    /// </summary>
    internal sealed class SeekIndexEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SeekIndexEntry"/> class.
        /// </summary>
        /// <param name="frame">The frame.</param>
        internal SeekIndexEntry(MediaFrame frame)
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
        public override string ToString()
        {
            return $"IX: {StreamIndex,3} | TM: {StartTime} | PTS: {PresentationTime} | DTS: {DecodingTime}";
        }
    }
}
