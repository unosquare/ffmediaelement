namespace Unosquare.FFME
{
    using System;

    /// <summary>
    /// Represents the event arguments of the MediaOpening routed event.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class MediaOpeningEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaOpeningEventArgs"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="options">The options.</param>
        /// <param name="info">The information.</param>
        public MediaOpeningEventArgs(object source, MediaOptions options, MediaInfo info)
        {
            Source = source;
            Options = options;
            Info = info;
        }

        /// <summary>
        /// Gets source.
        /// </summary>
        public object Source { get; }

        /// <summary>
        /// Set or change the options before the media is opened.
        /// </summary>
        public MediaOptions Options { get; }

        /// <summary>
        /// Provides internal details of the media, inclusing its component streams.
        /// Typically, options are set based on what this information contains.
        /// </summary>
        public MediaInfo Info { get; }
    }
}
