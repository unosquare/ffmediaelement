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
            if (Parent == null || sender == null) return;

            GuiContext.Current.EnqueueInvoke(async () =>
            {
                Parent.PostMediaEndedEvent();

                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (Parent.UnloadedBehavior == System.Windows.Controls.MediaState.Close)
                {
                    await sender.Close();
                }
                else if (Parent.UnloadedBehavior == System.Windows.Controls.MediaState.Play)
                {
                    await sender.Stop();
                    await sender.Play();
                }
                else if (Parent.UnloadedBehavior == System.Windows.Controls.MediaState.Stop)
                {
                    await sender.Stop();
                }
            });
        }

        /// <inheritdoc />
        public void OnMediaFailed(MediaEngine sender, Exception e) =>
            Parent?.PostMediaFailedEvent(e);

        /// <inheritdoc />
        public void OnMediaOpened(MediaEngine sender, MediaInfo mediaInfo)
        {
            if (Parent == null || sender == null) return;

            GuiContext.Current.EnqueueInvoke(async () =>
            {
                // Set initial controller properties
                // Has to be on the GUI thread as we are reading dependency properties
                sender.State.Volume = Parent.Volume;
                sender.State.IsMuted = Parent.IsMuted;
                sender.State.Balance = Parent.Balance;

                // Notify the end user media has opened successfully
                Parent.PostMediaOpenedEvent(mediaInfo);

                // Start playback if we don't support pausing
                if (sender.State.CanPause == false)
                {
                    await sender.Play();
                    return;
                }

                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (Parent.LoadedBehavior == System.Windows.Controls.MediaState.Play)
                    await sender.Play();
                else if (Parent.LoadedBehavior == System.Windows.Controls.MediaState.Pause)
                    await sender.Pause();
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
