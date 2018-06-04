namespace Unosquare.FFME.Platform
{
    using Shared;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// The Media engine connector
    /// </summary>
    /// <seealso cref="IMediaConnector" />
    internal class WindowsMediaConnector : IMediaConnector
    {
        private MediaElement Parent = null;

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
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task OnBufferingEnded(MediaEngine sender)
        {
            return Parent != null ? Parent.RaiseBufferingEndedEvent() : Task.CompletedTask;
        }

        /// <summary>
        /// Called when [buffering started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task OnBufferingStarted(MediaEngine sender)
        {
            return Parent != null ? Parent.RaiseBufferingStartedEvent() : Task.CompletedTask;
        }

        /// <summary>
        /// Called when [media closed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task OnMediaClosed(MediaEngine sender)
        {
            return Parent != null ? Parent.RaiseMediaClosedEvent() : Task.CompletedTask;
        }

        /// <summary>
        /// Called when [media ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task OnMediaEnded(MediaEngine sender)
        {
            if (Parent == null) return Task.CompletedTask;

            return GuiContext.Current.EnqueueInvoke(async () =>
            {
                await Parent.RaiseMediaEndedEvent();
                switch (Parent.UnloadedBehavior)
                {
                    case System.Windows.Controls.MediaState.Close:
                        {
                            await sender.Close();
                            break;
                        }

                    case System.Windows.Controls.MediaState.Play:
                        {
                            await sender.Stop().ContinueWith(async (t) => await sender.Play());
                            break;
                        }

                    case System.Windows.Controls.MediaState.Stop:
                        {
                            await sender.Stop();
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
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task OnMediaFailed(MediaEngine sender, Exception e)
        {
            return Parent != null ? Parent.RaiseMediaFailedEvent(e) : Task.CompletedTask;
        }

        /// <summary>
        /// Called when [media opened].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task OnMediaOpened(MediaEngine sender)
        {
            if (Parent == null) return Task.CompletedTask;

            return GuiContext.Current.EnqueueInvoke(async () =>
            {
                await Parent.RaiseMediaOpenedEvent();
                if (sender.State.CanPause == false)
                {
                    await sender.Play();
                    return;
                }

                switch (Parent.LoadedBehavior)
                {
                    case System.Windows.Controls.MediaState.Play:
                        {
                            await sender.Play();
                            break;
                        }
                    case System.Windows.Controls.MediaState.Pause:
                        {
                            await sender.Pause();
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
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task OnMediaOpening(MediaEngine sender, MediaOptions options, MediaInfo mediaInfo)
        {
            return Parent != null ? Parent.RaiseMediaOpeningEvent(options, mediaInfo) : Task.CompletedTask;
        }

        /// <summary>
        /// Called when media options are changing.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="options">The options.</param>
        /// <param name="mediaInfo">The media information.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task OnMediaChanging(MediaEngine sender, MediaOptions options, MediaInfo mediaInfo)
        {
            return Parent != null ? Parent.RaiseMediaChangingEvent(options, mediaInfo) : Task.CompletedTask;
        }

        /// <summary>
        /// Called when [media initializing].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="config">The container configuration options.</param>
        /// <param name="url">The URL.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task OnMediaInitializing(MediaEngine sender, ContainerConfiguration config, string url)
        {
            return Parent != null ? Parent.RaiseMediaInitializingEvent(config, url) : Task.CompletedTask;
        }

        /// <summary>
        /// Called when [message logged].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="T:Unosquare.FFME.Shared.MediaLogMessage" /> instance containing the event data.</param>
        public void OnMessageLogged(MediaEngine sender, MediaLogMessage e)
        {
            Parent?.RaiseMessageLoggedEvent(e);
        }

        /// <summary>
        /// Called when [seeking ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task OnSeekingEnded(MediaEngine sender)
        {
            return Parent != null ? Parent.RaiseSeekingEndedEvent() : Task.CompletedTask;
        }

        /// <summary>
        /// Called when [seeking started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task OnSeekingStarted(MediaEngine sender)
        {
            return Parent != null ? Parent.RaiseSeekingStartedEvent() : Task.CompletedTask;
        }

        /// <summary>
        /// Called when [position changed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        public void OnPositionChanged(MediaEngine sender, TimeSpan oldValue, TimeSpan newValue)
        {
            if (Parent == null) return;

            // Only set a reportable position if we are playing and not seeking
            if (sender.State.IsPlaying && sender.State.IsSeeking == false)
                Parent.ReportablePosition = newValue;

            Parent.RaisePositionChangedEvent(oldValue, newValue);
        }

        /// <summary>
        /// Called when [media state changed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task OnMediaStateChanged(MediaEngine sender, PlaybackStatus oldValue, PlaybackStatus newValue)
        {
            if (Parent == null) return Task.CompletedTask;

            // Force a reportable position when the media state changes
            Parent.ReportablePosition = sender.State.Position;
            return Parent.RaiseMediaStateChangedEvent(
                (System.Windows.Controls.MediaState)oldValue,
                (System.Windows.Controls.MediaState)newValue);
        }

        #endregion
    }
}
