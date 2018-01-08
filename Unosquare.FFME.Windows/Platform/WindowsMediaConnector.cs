namespace Unosquare.FFME.Platform
{
    using Shared;
    using System;

    /// <summary>
    /// The Media engine connector
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Shared.IMediaConnector" />
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

        /// <summary>
        /// Called when [buffering ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnBufferingEnded(object sender)
        {
            Control?.RaiseBufferingEndedEvent();
        }

        /// <summary>
        /// Called when [buffering started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnBufferingStarted(object sender)
        {
            Control?.RaiseBufferingStartedEvent();
        }

        /// <summary>
        /// Called when [media closed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnMediaClosed(object sender)
        {
            Control?.RaiseMediaClosedEvent();
        }

        /// <summary>
        /// Called when [media ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnMediaEnded(object sender)
        {
            Control?.RaiseMediaEndedEvent();
        }

        /// <summary>
        /// Called when [media failed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        public void OnMediaFailed(object sender, Exception e)
        {
            Control?.RaiseMediaFailedEvent(e);
        }

        /// <summary>
        /// Called when [media opened].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnMediaOpened(object sender)
        {
            Control?.RaiseMediaOpenedEvent();
        }

        /// <summary>
        /// Called when [media opening].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="mediaOptions">The media options.</param>
        /// <param name="mediaInfo">The media information.</param>
        public void OnMediaOpening(object sender, MediaOptions mediaOptions, MediaInfo mediaInfo)
        {
            Control?.RaiseMediaOpeningEvent(mediaOptions, mediaInfo);
        }

        /// <summary>
        /// Called when [message logged].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="T:Unosquare.FFME.Shared.MediaLogMessage" /> instance containing the event data.</param>
        public void OnMessageLogged(object sender, MediaLogMessage e)
        {
            Control?.RaiseMessageLoggedEvent(e);
        }

        /// <summary>
        /// Called when [position changed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="position">The position.</param>
        public void OnPositionChanged(object sender, TimeSpan position)
        {
            Control?.RaisePositionChangedEvent(position);
        }

        /// <summary>
        /// Called when an underlying media engine property is changed.
        /// This is used to handle property change notifications
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="propertyName">Name of the property.</param>
        public void OnPropertyChanged(object sender, string propertyName)
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

        /// <summary>
        /// Called when [seeking ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnSeekingEnded(object sender)
        {
            Control?.RaiseSeekingEndedEvent();
        }

        /// <summary>
        /// Called when [seeking started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        public void OnSeekingStarted(object sender)
        {
            Control?.RaiseSeekingStartedEvent();
        }
    }
}
