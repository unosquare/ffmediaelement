namespace Unosquare.FFME.Events
{
    using System;

    /// <summary>
    /// Contains the media state changed event args.
    /// </summary>
    /// <seealso cref="EventArgs" />
    public class MediaStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="oldState">State of the previous.</param>
        /// <param name="newState">The new state.</param>
        internal MediaStateChangedEventArgs(MediaPlaybackState oldState, MediaPlaybackState newState)
        {
            OldMediaState = oldState;
            MediaState = newState;
        }

        /// <summary>
        /// Gets the current media state.
        /// </summary>
        public MediaPlaybackState MediaState { get; }

        /// <summary>
        /// Gets the position.
        /// </summary>
        public MediaPlaybackState OldMediaState { get; }
    }
}
