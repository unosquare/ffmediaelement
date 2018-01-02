namespace Unosquare.FFME.Core
{
    using System;
    using System.ComponentModel;

    internal class WindowsEventConnector : IEventConnector
    {
        private MediaElement Control = null;

        public WindowsEventConnector(MediaElement control)
        {
            Control = control;
        }

        public void OnBufferingEnded(object sender, EventArgs e)
        {
            Control?.RaiseBufferingEndedEvent();
        }

        public void OnBufferingStarted(object sender, EventArgs e)
        {
            Control?.RaiseBufferingStartedEvent();
        }

        public void OnMediaClosed(object sender, EventArgs e)
        {
            Control?.RaiseMediaClosedEvent();
        }

        public void OnMediaEnded(object sender, EventArgs e)
        {
            Control?.RaiseMediaEndedEvent();
        }

        public void OnMediaFailed(object sender, ExceptionEventArgs e)
        {
            Control?.RaiseMediaFailedEvent(e.Exception);
        }

        public void OnMediaOpened(object sender, EventArgs e)
        {
            Control?.RaiseMediaOpenedEvent();
        }

        public void OnMediaOpening(object sender, MediaOpeningEventArgs e)
        {
            Control?.RaiseMediaOpeningEvent();
        }

        public void OnMessageLogged(object sender, MediaLogMessagEventArgs e)
        {
            Control?.RaiseMessageLoggedEvent(e);
        }

        public void OnPositionChanged(object sender, PositionChangedEventArgs e)
        {
            Control?.RaisePositionChangedEvent(e);
        }

        public void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                // forward internal changes to the MediaElement dependency Properties
                case nameof(MediaElementCore.Source):
                    Control.Source = Control.MediaCore.Source;
                    break;
                case nameof(MediaElementCore.LoadedBehavior):
                    Control.LoadedBehavior = (System.Windows.Controls.MediaState)Control.MediaCore.LoadedBehavior;
                    break;
                case nameof(MediaElementCore.SpeedRatio):
                    Control.SpeedRatio = Control.MediaCore.SpeedRatio;
                    break;
                case nameof(MediaElementCore.UnloadedBehavior):
                    Control.UnloadedBehavior = (System.Windows.Controls.MediaState)Control.MediaCore.UnloadedBehavior;
                    break;
                case nameof(MediaElementCore.Volume):
                    Control.Volume = Control.MediaCore.Volume;
                    break;
                case nameof(MediaElementCore.Balance):
                    Control.Balance = Control.MediaCore.Balance;
                    break;
                case nameof(MediaElementCore.IsMuted):
                    Control.IsMuted = Control.MediaCore.IsMuted;
                    break;
                case nameof(MediaElementCore.ScrubbingEnabled):
                    Control.ScrubbingEnabled = Control.MediaCore.ScrubbingEnabled;
                    break;
                case nameof(MediaElementCore.Position):
                    Control.Position = Control.MediaCore.Position;
                    break;

                // Simply forward notification of same-named properties
                case nameof(MediaElementCore.IsOpen):
                case nameof(MediaElementCore.IsOpening):
                case nameof(MediaElementCore.MediaFormat):
                case nameof(MediaElementCore.HasAudio):
                case nameof(MediaElementCore.HasVideo):
                case nameof(MediaElementCore.VideoCodec):
                case nameof(MediaElementCore.VideoBitrate):
                case nameof(MediaElementCore.NaturalVideoWidth):
                case nameof(MediaElementCore.NaturalVideoHeight):
                case nameof(MediaElementCore.VideoFrameRate):
                case nameof(MediaElementCore.VideoFrameLength):
                case nameof(MediaElementCore.AudioCodec):
                case nameof(MediaElementCore.AudioBitrate):
                case nameof(MediaElementCore.AudioChannels):
                case nameof(MediaElementCore.AudioSampleRate):
                case nameof(MediaElementCore.AudioBitsPerSample):
                case nameof(MediaElementCore.NaturalDuration):
                case nameof(MediaElementCore.CanPause):
                case nameof(MediaElementCore.IsLiveStream):
                case nameof(MediaElementCore.IsSeekable):
                case nameof(MediaElementCore.BufferCacheLength):
                case nameof(MediaElementCore.DownloadCacheLength):
                case nameof(MediaElementCore.FrameStepDuration):
                case nameof(MediaElementCore.MediaState):
                case nameof(MediaElementCore.IsBuffering):
                case nameof(MediaElementCore.BufferingProgress):
                case nameof(MediaElementCore.IsPlaying):
                case nameof(MediaElementCore.DownloadProgress):
                case nameof(MediaElementCore.HasMediaEnded):
                case nameof(MediaElementCore.IsSeeking):
                case nameof(MediaElementCore.IsPositionUpdating):
                case nameof(MediaElementCore.Metadata):
                    Control?.RaisePropertyChangedEvent(e.PropertyName);
                    break;
            }
        }

        public void OnSeekingEnded(object sender, EventArgs e)
        {
            Control?.RaiseSeekingEndedEvent();
        }

        public void OnSeekingStarted(object sender, EventArgs e)
        {
            Control?.RaiseSeekingStartedEvent();
        }
    }
}
