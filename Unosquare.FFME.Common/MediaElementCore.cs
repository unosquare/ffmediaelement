namespace Unosquare.FFME
{
    using System;

    public class MediaElementCore
    {
        #region Constructors

        public MediaElementCore(object parent, bool isInDesignTime)
        {

        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a logging message from the FFmpeg library has been received.
        /// This is shared across all instances of Media Elements
        /// </summary>
        public static event EventHandler<MediaLogMessagEventArgs> FFmpegMessageLogged;

        /// <summary>
        /// Occurs when a logging message has been logged.
        /// This does not include FFmpeg messages.
        /// </summary>
        public event EventHandler<MediaLogMessagEventArgs> MessageLogged;

        #endregion

        #region Methods

        /// <summary>
        /// Raises the FFmpegMessageLogged event
        /// </summary>
        /// <param name="eventArgs">The <see cref="MediaLogMessagEventArgs" /> instance containing the event data.</param>
        internal static void RaiseFFmpegMessageLogged(MediaLogMessagEventArgs eventArgs)
        {
            FFmpegMessageLogged?.Invoke(typeof(MediaElementCore), eventArgs);
        }

        #endregion

        #region Logging Events

        /// <summary>
        /// Raises the MessageLogged event
        /// </summary>
        /// <param name="eventArgs">The <see cref="MediaLogMessagEventArgs" /> instance containing the event data.</param>
        internal void RaiseMessageLogged(MediaLogMessagEventArgs eventArgs)
        {
            MessageLogged?.Invoke(this, eventArgs);
        }

        #endregion
    }
}
