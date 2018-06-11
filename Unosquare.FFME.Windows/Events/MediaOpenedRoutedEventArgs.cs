namespace Unosquare.FFME.Events
{
    using Shared;
    using System.Windows;

    /// <summary>
    /// Represents the event arguments of the <see cref="MediaElement.MediaOpened"/> or
    /// <see cref="MediaElement.MediaChanged"/> routed events.
    /// </summary>
    /// <seealso cref="RoutedEventArgs" />
    public class MediaOpenedRoutedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaOpenedRoutedEventArgs" /> class.
        /// </summary>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="source">The source.</param>
        /// <param name="info">The input information.</param>
        public MediaOpenedRoutedEventArgs(RoutedEvent routedEvent, object source, MediaInfo info)
            : base(routedEvent, source)
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
