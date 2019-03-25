namespace Unosquare.FFME.Events
{
    using Engine;
    using System;
    using MediaElement = MediaElement;
#if WINDOWS_UWP
    using Windows.UI.Xaml.Media;
#else
    using System.Windows.Controls;
#endif

    /// <summary>
    /// Contains the media state changed event args
    /// </summary>
    /// <seealso cref="EventArgs" />
    public class MediaStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="oldState">State of the previous.</param>
        /// <param name="newState">The new state.</param>
        public MediaStateChangedEventArgs(PlaybackStatus oldState, PlaybackStatus newState)
        {
            OldMediaState = MediaElement.PlaybackStatusToMediaState(oldState);
            MediaState = MediaElement.PlaybackStatusToMediaState(newState);
        }

#if WINDOWS_UWP
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
