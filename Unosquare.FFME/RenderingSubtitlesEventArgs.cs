namespace Unosquare.FFME
{
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The subtitle rendering event args
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class RenderingSubtitlesEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingSubtitlesEventArgs" /> class.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="originalText">The original text.</param>
        /// <param name="format">The format.</param>
        /// <param name="position">The position.</param>
        public RenderingSubtitlesEventArgs(List<string> text, List<string> originalText, AVSubtitleType format, TimeSpan position)
            : base()
        {
            Text = text;
            Format = format;
            Position = position;
            OriginalText = originalText;
        }

        /// <summary>
        /// Gets the text stripped out of ASS or SRT formatting.
        /// </summary>
        public List<string> Text { get; }

        /// <summary>
        /// Gets the original text.
        /// </summary>
        public List<string> OriginalText { get; }

        /// <summary>
        /// Gets the format.
        /// </summary>
        public AVSubtitleType Format { get; }

        /// <summary>
        /// Gets the position.
        /// </summary>
        public TimeSpan Position { get; }
    }
}
