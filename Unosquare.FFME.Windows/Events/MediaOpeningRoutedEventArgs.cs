namespace Unosquare.FFME.Events
{
    using Shared;
    using System.Windows;

    /// <summary>
    /// Represents the event arguments of the MediaOpening or MediaChanging routed events.
    /// </summary>
    /// <seealso cref="RoutedEventArgs" />
    public class MediaOpeningRoutedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaOpeningRoutedEventArgs" /> class.
        /// </summary>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="source">The source.</param>
        /// <param name="options">The options.</param>
        /// <param name="info">The input information.</param>
        public MediaOpeningRoutedEventArgs(RoutedEvent routedEvent, object source, MediaOptions options, MediaInfo info)
            : base(routedEvent, source)
        {
            Options = options;
            Info = info;
        }

        /// <summary>
        /// Set or change the options before the media is opened.
        /// </summary>
        public MediaOptions Options { get; }

        /// <summary>
        /// Provides internal details of the media, including its component streams.
        /// Typically, options are set based on what this information contains.
        /// </summary>
        public MediaInfo Info { get; }
    }
}
