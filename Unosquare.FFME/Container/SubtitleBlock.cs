namespace Unosquare.FFME.Container
{
    using FFmpeg.AutoGen;
    using System.Collections.Generic;

    /// <summary>
    /// A subtitle frame container. Simply contains text lines.
    /// </summary>
    internal sealed class SubtitleBlock : MediaBlock
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SubtitleBlock"/> class.
        /// </summary>
        internal SubtitleBlock()
            : base(MediaType.Subtitle)
        {
            // placeholder
        }

        #region Properties

        /// <summary>
        /// Gets the lines of text for this subtitle frame with all formatting stripped out.
        /// </summary>
        public List<string> Text { get; } = new List<string>(16);

        /// <summary>
        /// Gets the original text in SRT or ASS format.
        /// </summary>
        public List<string> OriginalText { get; } = new List<string>(16);

        /// <summary>
        /// Gets the type of the original text.
        /// Returns None when it's a bitmap or when it's None.
        /// </summary>
        public AVSubtitleType OriginalTextType { get; internal set; }

        #endregion
    }
}
