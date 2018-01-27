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
        public void OnBufferingEnded(MediaEngine sender)
        {
            Parent?.RaiseBufferingEndedEvent();
        }

        /// <summary>
        /// Called when [buffering started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnBufferingStarted(MediaEngine sender)
        {
            Parent?.RaiseBufferingStartedEvent();
        }

        /// <summary>
        /// Called when [media closed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnMediaClosed(MediaEngine sender)
        {
            Parent?.RaiseMediaClosedEvent();
        }

        /// <summary>
        /// Called when [media ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnMediaEnded(MediaEngine sender)
        {
            if (Parent == null) return;

            Parent.RaiseMediaEndedEvent();
            GuiContext.Current.Invoke(async () =>
            {
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
        public void OnMediaFailed(MediaEngine sender, Exception e)
        {
            Parent?.RaiseMediaFailedEvent(e);
        }

        /// <summary>
        /// Called when [media opened].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnMediaOpened(MediaEngine sender)
        {
            if (Parent == null) return;

            Parent.RaiseMediaOpenedEvent();
            GuiContext.Current.Invoke(async () =>
            {
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
        /// <param name="mediaOptions">The media options.</param>
        /// <param name="mediaInfo">The media information.</param>
        public void OnMediaOpening(MediaEngine sender, MediaOptions mediaOptions, MediaInfo mediaInfo)
        {
            Parent?.RaiseMediaOpeningEvent(mediaOptions, mediaInfo);
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
        public void OnSeekingEnded(MediaEngine sender)
        {
            Parent?.RaiseSeekingEndedEvent();
        }

        /// <summary>
        /// Called when [seeking started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnSeekingStarted(MediaEngine sender)
        {
            Parent?.RaiseSeekingStartedEvent();
        }

        #endregion
    }
}
