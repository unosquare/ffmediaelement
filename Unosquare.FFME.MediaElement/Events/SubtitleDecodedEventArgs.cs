namespace Unosquare.FFME.Events
{
    using FFmpeg.AutoGen;

    /// <summary>
    /// Event arguments corresponding to the subtitle decoded event. Useful for capturing streams.
    /// </summary>
    public sealed unsafe class SubtitleDecodedEventArgs : InputFormatEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SubtitleDecodedEventArgs"/> class.
        /// </summary>
        /// <param name="subtitle">The subtitle pointer.</param>
        /// <param name="context">The input format context.</param>
        internal SubtitleDecodedEventArgs(AVSubtitle* subtitle, AVFormatContext* context)
            : base(context)
        {
            Subtitle = subtitle;
        }

        /// <summary>
        /// Gets the pointer to subtitle that was decoded.
        /// </summary>
        public AVSubtitle* Subtitle { get; }
    }
}
