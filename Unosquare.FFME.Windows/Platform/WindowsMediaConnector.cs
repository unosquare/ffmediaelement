namespace Unosquare.FFME.Platform
{
    using Shared;
    using System;

    /// <summary>
    /// The Media engine connector
    /// </summary>
    /// <seealso cref="IMediaConnector" />
    internal class WindowsMediaConnector : IMediaConnector
    {
        private MediaElement Control = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsMediaConnector"/> class.
        /// </summary>
        /// <param name="control">The control.</param>
        public WindowsMediaConnector(MediaElement control)
        {
            Control = control;
        }

        #region Event Signal Handling

        /// <summary>
        /// Called when [buffering ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnBufferingEnded(MediaEngine sender)
        {
            Control?.RaiseBufferingEndedEvent();
        }

        /// <summary>
        /// Called when [buffering started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnBufferingStarted(MediaEngine sender)
        {
            Control?.RaiseBufferingStartedEvent();
        }

        /// <summary>
        /// Called when [media closed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnMediaClosed(MediaEngine sender)
        {
            Control?.RaiseMediaClosedEvent();
        }

        /// <summary>
        /// Called when [media ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnMediaEnded(MediaEngine sender)
        {
            Control?.RaiseMediaEndedEvent();
        }

        /// <summary>
        /// Called when [media failed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        public void OnMediaFailed(MediaEngine sender, Exception e)
        {
            Control?.RaiseMediaFailedEvent(e);
        }

        /// <summary>
        /// Called when [media opened].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnMediaOpened(MediaEngine sender)
        {
            Control?.RaiseMediaOpenedEvent();
        }

        /// <summary>
        /// Called when [media opening].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="mediaOptions">The media options.</param>
        /// <param name="mediaInfo">The media information.</param>
        public void OnMediaOpening(MediaEngine sender, MediaOptions mediaOptions, MediaInfo mediaInfo)
        {
            Control?.RaiseMediaOpeningEvent(mediaOptions, mediaInfo);
        }

        /// <summary>
        /// Called when [message logged].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="T:Unosquare.FFME.Shared.MediaLogMessage" /> instance containing the event data.</param>
        public void OnMessageLogged(MediaEngine sender, MediaLogMessage e)
        {
            Control?.RaiseMessageLoggedEvent(e);
        }

        /// <summary>
        /// Called when [seeking ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnSeekingEnded(MediaEngine sender)
        {
            Control?.RaiseSeekingEndedEvent();
        }

        /// <summary>
        /// Called when [seeking started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnSeekingStarted(MediaEngine sender)
        {
            Control?.RaiseSeekingStartedEvent();
        }

        #endregion
    }
}
