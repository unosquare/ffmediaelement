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
        /// Called when [position changed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="position">The position.</param>
        public void OnPositionChanged(MediaEngine sender, TimeSpan position)
        {
            Control?.RaisePositionChangedEvent(position);
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

        /// <summary>
        /// Called when an underlying media engine property is changed.
        /// This is used to handle property change notifications
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="propertyNames">Name of the property.</param>
        public void OnPropertiesChanged(MediaEngine sender, string[] propertyNames)
        {
            // TODO: bug sometimes continuously resizing the window causes everything to freeze.
            // This might be because of excessive property change notifications. It might be a good idea
            // to notify everything at once every say, 25ms.
            // Either that or attach to the properties from the mediaelement via WPF binding.
            if (propertyNames.Length == 0) return;
            return;
            WindowsPlatform.Instance.Gui?.Invoke(() =>
            {
                foreach (var propertyName in propertyNames)
                {
                    switch (propertyName)
                    {
                        // forward internal changes to the MediaElement dependency Properties
                        case nameof(MediaEngine.Source):
                            Control.Source = Control.MediaCore.Source;
                            break;
                        case nameof(MediaEngine.LoadedBehavior):
                            Control.LoadedBehavior = (System.Windows.Controls.MediaState)Control.MediaCore.LoadedBehavior;
                            break;
                        case nameof(MediaEngine.SpeedRatio):
                            Control.SpeedRatio = Control.MediaCore.SpeedRatio;
                            break;
                        case nameof(MediaEngine.UnloadedBehavior):
                            Control.UnloadedBehavior = (System.Windows.Controls.MediaState)Control.MediaCore.UnloadedBehavior;
                            break;
                        case nameof(MediaEngine.Volume):
                            Control.Volume = Control.MediaCore.Volume;
                            break;
                        case nameof(MediaEngine.Balance):
                            Control.Balance = Control.MediaCore.Balance;
                            break;
                        case nameof(MediaEngine.IsMuted):
                            Control.IsMuted = Control.MediaCore.IsMuted;
                            break;
                        case nameof(MediaEngine.ScrubbingEnabled):
                            Control.ScrubbingEnabled = Control.MediaCore.ScrubbingEnabled;
                            break;
                        case nameof(MediaEngine.Position):
                            Control.Position = Control.MediaCore.Position;
                            break;
                        default:
                            // Simply forward notification of same-named properties
                            Control?.RaisePropertyChangedEvent(propertyName);
                            break;
                    }
                }
            });
        }
    }
}
