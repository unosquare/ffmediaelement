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
        /// <param name="previousState">State of the previous.</param>
        /// <param name="newState">The new state.</param>
        public MediaStateChangedRoutedEventArgs(RoutedEvent routedEvent, object source, MediaState previousState, MediaState newState)
            : base(routedEvent, source)
        {
            PreviousState = previousState;
            NewState = newState;
        }

        /// <summary>
        /// Gets the position.
        /// </summary>
        public MediaState PreviousState { get; }

        /// <summary>
        /// Gets the new state.
        /// </summary>
        public MediaState NewState { get; }
    }
}
