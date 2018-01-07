namespace Unosquare.FFME.Events
{
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides the subtitles rendering payload as event arguments.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public sealed class RenderingSubtitlesEventArgs : RenderingEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingSubtitlesEventArgs" /> class.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="originalText">The original text.</param>
        /// <param name="format">The format.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="startTime">The start time.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="clock">The clock.</param>
        internal RenderingSubtitlesEventArgs(
            List<string> text,
            List<string> originalText,
            AVSubtitleType format,
            StreamInfo stream,
            TimeSpan startTime,
            TimeSpan duration,
            TimeSpan clock)
            : base(stream, startTime, duration, clock)
        {
            Text = text;
            Format = format;
            OriginalText = originalText;
        }

        /// <summary>
        /// Gets the text stripped out of ASS or SRT formatting.
        /// This is what the default subtitle renderer will display
        /// on the screen.
        /// </summary>
        public List<string> Text { get; }

        /// <summary>
        /// Gets the text as originally decoded including
        /// all markup and formatting.
        /// </summary>
        public List<string> OriginalText { get; }

        /// <summary>
        /// Gets the type of subtitle format the original
        /// subtitle text is in.
        /// </summary>
        public AVSubtitleType Format { get; }

        /// <summary>
        /// When set to true, clears the current subtitle and 
        /// prevents the subtitle block from being rendered.
        /// </summary>
        public bool Cancel { get; set; }
    }
}
