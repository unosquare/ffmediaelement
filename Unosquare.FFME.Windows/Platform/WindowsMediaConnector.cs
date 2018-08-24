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

        /// <inheritdoc />
        public void OnBufferingEnded(MediaEngine sender) =>
            Parent?.PostBufferingEndedEvent();

        /// <inheritdoc />
        public void OnBufferingStarted(MediaEngine sender) =>
            Parent?.PostBufferingStartedEvent();

        /// <inheritdoc />
        public void OnMediaClosed(MediaEngine sender) =>
            Parent?.PostMediaClosedEvent();

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void OnMediaFailed(MediaEngine sender, Exception e) =>
            Parent?.PostMediaFailedEvent(e);

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void OnMediaOpening(MediaEngine sender, MediaOptions options, MediaInfo mediaInfo) =>
            Parent?.RaiseMediaOpeningEvent(options, mediaInfo);

        /// <inheritdoc />
        public void OnMediaChanging(MediaEngine sender, MediaOptions options, MediaInfo mediaInfo) =>
            Parent?.RaiseMediaChangingEvent(options, mediaInfo);

        /// <inheritdoc />
        public void OnMediaChanged(MediaEngine sender, MediaInfo mediaInfo) =>
            Parent?.PostMediaChangedEvent(mediaInfo);

        /// <inheritdoc />
        public void OnMediaInitializing(MediaEngine sender, ContainerConfiguration config, string url) =>
            Parent?.RaiseMediaInitializingEvent(config, url);

        /// <inheritdoc />
        public void OnMessageLogged(MediaEngine sender, MediaLogMessage e) =>
            Parent?.RaiseMessageLoggedEvent(e);

        /// <inheritdoc />
        public void OnSeekingEnded(MediaEngine sender) =>
            Parent?.PostSeekingEndedEvent();

        /// <inheritdoc />
        public void OnSeekingStarted(MediaEngine sender) =>
            Parent?.PostSeekingStartedEvent();

        /// <inheritdoc />
        public void OnPositionChanged(MediaEngine sender, TimeSpan oldValue, TimeSpan newValue)
        {
            Parent?.PostPositionChangedEvent(oldValue, newValue);
        }

        /// <inheritdoc />
        public void OnMediaStateChanged(MediaEngine sender, PlaybackStatus oldValue, PlaybackStatus newValue)
        {
            Parent?.PostMediaStateChangedEvent(
                (System.Windows.Controls.MediaState)oldValue,
                (System.Windows.Controls.MediaState)newValue);
        }

        #endregion
    }
}
