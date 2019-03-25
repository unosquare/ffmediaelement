namespace Unosquare.FFME.Events
{
    using Engine;
    using System;

    /// <summary>
    /// Represents the event arguments of the <see cref="MediaElement.MediaOpened"/> or
    /// <see cref="MediaElement.MediaChanged"/> routed events.
    /// </summary>
    /// <seealso cref="EventArgs" />
    public class MediaOpenedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaOpenedEventArgs" /> class.
        /// </summary>
        /// <param name="info">The input information.</param>
        public MediaOpenedEventArgs(MediaInfo info)
            : base()
        {
            Info = info;
        }

        /// <summary>
        /// Provides internal details of the media, including its component streams.
        /// Typically, options are set based on what this information contains.
        /// </summary>
        public MediaInfo Info { get; }
    }
}
