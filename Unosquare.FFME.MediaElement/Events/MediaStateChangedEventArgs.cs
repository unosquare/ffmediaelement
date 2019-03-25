namespace Unosquare.FFME.Events
{
    using Engine;
    using System;
#if WINDOWS_UWP
    using MediaState = Engine.PlaybackStatus;
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
            OldMediaState = (MediaState)oldState;
            MediaState = (MediaState)newState;
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
