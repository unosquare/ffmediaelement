namespace Unosquare.FFME.Decoding
{
    using Core;
    using System.Collections.Generic;

    /// <summary>
    /// A subtitle frame container. Simply contains text lines.
    /// </summary>
    internal sealed class SubtitleBlock : MediaBlock
    {
        #region Properties

        /// <summary>
        /// Gets the media type of the data
        /// </summary>
        public override MediaType MediaType => MediaType.Subtitle;

        /// <summary>
        /// Gets the lines of text for this subtitle frame.
        /// </summary>
        public List<string> Text { get; } = new List<string>(16);

        #endregion

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            // nothing to dispose.
        }
    }
}
