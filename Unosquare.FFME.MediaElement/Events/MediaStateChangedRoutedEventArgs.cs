namespace Unosquare.FFME.Events
{
    using Engine;

#if WINDOWS_UWP
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
#else
    using System.Windows;
    using System.Windows.Controls;
#endif

    /// <summary>
    /// Contains the media state changed routed event args
    /// </summary>
    /// <seealso cref="RoutedEventArgs" />
    public class MediaStateChangedRoutedEventArgs : RoutedEventArgs
    {
#if WINDOWS_UWP
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaStateChangedRoutedEventArgs"/> class.
        /// </summary>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="source">The source.</param>
        /// <param name="oldState">State of the previous.</param>
        /// <param name="newState">The new state.</param>
        public MediaStateChangedRoutedEventArgs(RoutedEvent routedEvent, object source, PlaybackStatus oldState, PlaybackStatus newState)
        {
            OldMediaState = MediaElement.PlaybackStatusToMediaState(oldState);
            MediaState = MediaElement.PlaybackStatusToMediaState(newState);
        }

        /// <summary>
        /// Gets the current media state.
        /// </summary>
        public MediaElementState MediaState { get; }

        /// <summary>
        /// Gets the position.
        /// </summary>
        public MediaElementState OldMediaState { get; }
#else
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaStateChangedRoutedEventArgs"/> class.
        /// </summary>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="source">The source.</param>
        /// <param name="oldState">State of the previous.</param>
        /// <param name="newState">The new state.</param>
        public MediaStateChangedRoutedEventArgs(RoutedEvent routedEvent, object source, PlaybackStatus oldState, PlaybackStatus newState)
            : base(routedEvent, source)
        {
            OldMediaState = FFME.MediaElement.PlaybackStatusToMediaState(oldState);
            MediaState = FFME.MediaElement.PlaybackStatusToMediaState(newState);
        }

        /// <summary>
        /// Gets the current media state.
        /// </summary>
        public MediaState MediaState { get; }

        /// <summary>
        /// Gets the position.
        /// </summary>
        public MediaState OldMediaState { get; }
#endif
    }
}
