namespace Unosquare.FFME
{
    using System.Windows;

    /// <summary>
    /// Represents the event arguments of the MediaOpening routed event.
    /// </summary>
    /// <seealso cref="System.Windows.RoutedEventArgs" />
    public class MediaOpeningRoutedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaOpeningRoutedEventArgs" /> class.
        /// </summary>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="source">The source.</param>
        /// <param name="options">The options.</param>
        public MediaOpeningRoutedEventArgs(RoutedEvent routedEvent, object source, MediaOptions options)
            : base(routedEvent, source)
        {
            Options = options;
        }

        /// <summary>
        /// Set or change the options before the media is opened.
        /// </summary>
        public MediaOptions Options { get; }
    }
}
