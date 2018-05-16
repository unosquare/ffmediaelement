namespace Unosquare.FFME.Events
{
    using Shared;
    using System.Windows;

    /// <summary>
    /// Represents the event arguments of the MediaInitializing routed event.
    /// </summary>
    /// <seealso cref="RoutedEventArgs" />
    public class MediaInitializingRoutedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaInitializingRoutedEventArgs" /> class.
        /// </summary>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="source">The source.</param>
        /// <param name="config">The container configuration options.</param>
        /// <param name="url">The URL.</param>
        public MediaInitializingRoutedEventArgs(RoutedEvent routedEvent, object source, ContainerConfiguration config, string url)
            : base(routedEvent, source)
        {
            Configuration = config;
            Url = url;
        }

        /// <summary>
        /// Set or change the container configuration options before the media is opened.
        /// </summary>
        public ContainerConfiguration Configuration { get; }

        /// <summary>
        /// Gets the URL.
        /// </summary>
        public string Url { get; }
    }
}
