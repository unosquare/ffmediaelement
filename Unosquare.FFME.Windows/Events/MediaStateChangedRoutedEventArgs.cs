namespace Unosquare.FFME.Events
{
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Contains the media state changed routed event args
    /// </summary>
    /// <seealso cref="RoutedEventArgs" />
    public class MediaStateChangedRoutedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaStateChangedRoutedEventArgs"/> class.
        /// </summary>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="source">The source.</param>
        /// <param name="oldState">State of the previous.</param>
        /// <param name="newState">The new state.</param>
        public MediaStateChangedRoutedEventArgs(RoutedEvent routedEvent, object source, MediaState oldState, MediaState newState)
            : base(routedEvent, source)
        {
            OldMediaState = oldState;
            MediaState = newState;
        }

        /// <summary>
        /// Gets the current media state.
        /// </summary>
        public MediaState MediaState { get; }

        /// <summary>
        /// Gets the position.
        /// </summary>
        public MediaState OldMediaState { get; }
    }
}
