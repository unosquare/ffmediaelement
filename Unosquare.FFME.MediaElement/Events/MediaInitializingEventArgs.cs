namespace Unosquare.FFME.Events
{
    using Engine;
    using System;
    using System.Windows;

    /// <summary>
    /// Represents the event arguments of the MediaInitializing routed event.
    /// </summary>
    /// <seealso cref="RoutedEventArgs" />
    public class MediaInitializingEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaInitializingEventArgs" /> class.
        /// </summary>
        /// <param name="config">The container configuration options.</param>
        /// <param name="mediaSource">The URL.</param>
        internal MediaInitializingEventArgs(ContainerConfiguration config, string mediaSource)
        {
            Configuration = config;
            MediaSource = mediaSource;
        }

        /// <summary>
        /// Set or change the container configuration options before the media is opened.
        /// </summary>
        public ContainerConfiguration Configuration { get; }

        /// <summary>
        /// Gets the URL.
        /// </summary>
        public string MediaSource { get; }
    }
}
