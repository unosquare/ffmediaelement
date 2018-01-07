namespace Unosquare.FFME.Platform
{
    using Shared;
    using System;
    using System.ComponentModel;

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
        /// <param name="e">The <see cref="T:System.ComponentModel.PropertyChangedEventArgs" /> instance containing the event data.</param>
        public void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
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

                // Simply forward notification of same-named properties
                case nameof(MediaEngine.IsOpen):
                case nameof(MediaEngine.IsOpening):
                case nameof(MediaEngine.MediaFormat):
                case nameof(MediaEngine.HasAudio):
                case nameof(MediaEngine.HasVideo):
                case nameof(MediaEngine.VideoCodec):
                case nameof(MediaEngine.VideoBitrate):
                case nameof(MediaEngine.NaturalVideoWidth):
                case nameof(MediaEngine.NaturalVideoHeight):
                case nameof(MediaEngine.VideoFrameRate):
                case nameof(MediaEngine.VideoFrameLength):
                case nameof(MediaEngine.VideoSmtpeTimecode):
                case nameof(MediaEngine.VideoHardwareDecoder):
                case nameof(MediaEngine.AudioCodec):
                case nameof(MediaEngine.AudioBitrate):
                case nameof(MediaEngine.AudioChannels):
                case nameof(MediaEngine.AudioSampleRate):
                case nameof(MediaEngine.AudioBitsPerSample):
                case nameof(MediaEngine.NaturalDuration):
                case nameof(MediaEngine.CanPause):
                case nameof(MediaEngine.IsLiveStream):
                case nameof(MediaEngine.IsSeekable):
                case nameof(MediaEngine.BufferCacheLength):
                case nameof(MediaEngine.DownloadCacheLength):
                case nameof(MediaEngine.FrameStepDuration):
                case nameof(MediaEngine.MediaState):
                case nameof(MediaEngine.IsBuffering):
                case nameof(MediaEngine.BufferingProgress):
                case nameof(MediaEngine.IsPlaying):
                case nameof(MediaEngine.DownloadProgress):
                case nameof(MediaEngine.HasMediaEnded):
                case nameof(MediaEngine.IsSeeking):
                case nameof(MediaEngine.IsPositionUpdating):
                case nameof(MediaEngine.Metadata):
                    Control?.RaisePropertyChangedEvent(e.PropertyName);
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
