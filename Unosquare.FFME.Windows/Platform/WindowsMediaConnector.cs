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
        private readonly MediaElement Parent;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsMediaConnector"/> class.
        /// </summary>
        /// <param name="parent">The control.</param>
        public WindowsMediaConnector(MediaElement parent)
        {
            Parent = parent;
        }

        #region Event Signal Handling

        /// <summary>
        /// Called when [buffering ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnBufferingEnded(MediaEngine sender) =>
            Parent?.PostBufferingEndedEvent();

        /// <summary>
        /// Called when [buffering started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnBufferingStarted(MediaEngine sender) =>
            Parent?.PostBufferingStartedEvent();

        /// <summary>
        /// Called when [media closed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnMediaClosed(MediaEngine sender) =>
            Parent?.PostMediaClosedEvent();

        /// <summary>
        /// Called when [media ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnMediaEnded(MediaEngine sender)
        {
            if (Parent == null) return;

            GuiContext.Current.EnqueueInvoke(() =>
            {
                Parent?.PostMediaEndedEvent();
                switch (Parent?.UnloadedBehavior ?? System.Windows.Controls.MediaState.Manual)
                {
                    case System.Windows.Controls.MediaState.Close:
                        {
                            sender?.Close();
                            break;
                        }

                    case System.Windows.Controls.MediaState.Play:
                        {
                            sender?.Stop().ContinueWith((t) => sender?.Play());
                            break;
                        }

                    case System.Windows.Controls.MediaState.Stop:
                        {
                            sender?.Stop();
                            break;
                        }

                    default:
                        {
                            break;
                        }
                }
            });
        }

        /// <summary>
        /// Called when [media failed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        public void OnMediaFailed(MediaEngine sender, Exception e) =>
            Parent?.PostMediaFailedEvent(e);

        /// <summary>
        /// Called when [media opened].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="mediaInfo">The media information.</param>
        public void OnMediaOpened(MediaEngine sender, MediaInfo mediaInfo)
        {
            if (Parent == null) return;

            GuiContext.Current.EnqueueInvoke(() =>
            {
                Parent?.PostMediaOpenedEvent(mediaInfo);
                if ((sender?.State.CanPause ?? true) == false)
                {
                    sender?.Play();
                    return;
                }

                switch (Parent?.LoadedBehavior ?? System.Windows.Controls.MediaState.Manual)
                {
                    case System.Windows.Controls.MediaState.Play:
                        {
                            sender?.Play();
                            break;
                        }

                    case System.Windows.Controls.MediaState.Pause:
                        {
                            sender?.Pause();
                            break;
                        }

                    default:
                        {
                            break;
                        }
                }
            });
        }

        /// <summary>
        /// Called when [media opening].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="options">The media options.</param>
        /// <param name="mediaInfo">The media information.</param>
        public void OnMediaOpening(MediaEngine sender, MediaOptions options, MediaInfo mediaInfo) =>
            Parent?.RaiseMediaOpeningEvent(options, mediaInfo);

        /// <summary>
        /// Called when media options are changing.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="options">The options.</param>
        /// <param name="mediaInfo">The media information.</param>
        public void OnMediaChanging(MediaEngine sender, MediaOptions options, MediaInfo mediaInfo) =>
            Parent?.RaiseMediaChangingEvent(options, mediaInfo);

        /// <summary>
        /// Called when media options have been changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="mediaInfo">The media information.</param>
        public void OnMediaChanged(MediaEngine sender, MediaInfo mediaInfo) =>
            Parent?.PostMediaChangedEvent(mediaInfo);

        /// <summary>
        /// Called when [media initializing].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="config">The container configuration options.</param>
        /// <param name="url">The URL.</param>
        public void OnMediaInitializing(MediaEngine sender, ContainerConfiguration config, string url) =>
            Parent?.RaiseMediaInitializingEvent(config, url);

        /// <summary>
        /// Called when [message logged].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="T:Unosquare.FFME.Shared.MediaLogMessage" /> instance containing the event data.</param>
        public void OnMessageLogged(MediaEngine sender, MediaLogMessage e) =>
            Parent?.RaiseMessageLoggedEvent(e);

        /// <summary>
        /// Called when [seeking ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnSeekingEnded(MediaEngine sender) =>
            Parent?.PostSeekingEndedEvent();

        /// <summary>
        /// Called when [seeking started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnSeekingStarted(MediaEngine sender) =>
            Parent?.PostSeekingStartedEvent();

        /// <summary>
        /// Called when [position changed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        public void OnPositionChanged(MediaEngine sender, TimeSpan oldValue, TimeSpan newValue)
        {
            if (Parent == null) return;
            Parent?.PostPositionChangedEvent(oldValue, newValue);
        }

        /// <summary>
        /// Called when [media state changed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        public void OnMediaStateChanged(MediaEngine sender, PlaybackStatus oldValue, PlaybackStatus newValue)
        {
            if (Parent == null) return;

            Parent?.PostMediaStateChangedEvent(
                (System.Windows.Controls.MediaState)oldValue,
                (System.Windows.Controls.MediaState)newValue);
        }

        #endregion
    }
}
