namespace Unosquare.FFME.Platform
{
    using Shared;
    using System;
    using System.ComponentModel;

    internal class WindowsEventConnector : IMediaEventConnector
    {
        private MediaElement Control = null;

        public WindowsEventConnector(MediaElement control)
        {
            Control = control;
        }

        public void OnBufferingEnded(object sender)
        {
            Control?.RaiseBufferingEndedEvent();
        }

        public void OnBufferingStarted(object sender)
        {
            Control?.RaiseBufferingStartedEvent();
        }

        public void OnMediaClosed(object sender)
        {
            Control?.RaiseMediaClosedEvent();
        }

        public void OnMediaEnded(object sender)
        {
            Control?.RaiseMediaEndedEvent();
        }

        public void OnMediaFailed(object sender, Exception e)
        {
            Control?.RaiseMediaFailedEvent(e);
        }

        public void OnMediaOpened(object sender)
        {
            Control?.RaiseMediaOpenedEvent();
        }

        public void OnMediaOpening(object sender, MediaOptions mediaOptions, MediaInfo mediaInfo)
        {
            Control?.RaiseMediaOpeningEvent(mediaOptions, mediaInfo);
        }

        public void OnMessageLogged(object sender, MediaLogMessage e)
        {
            Control?.RaiseMessageLoggedEvent(e);
        }

        public void OnPositionChanged(object sender, TimeSpan position)
        {
            Control?.RaisePositionChangedEvent(position);
        }

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

        public void OnSeekingEnded(object sender)
        {
            Control?.RaiseSeekingEndedEvent();
        }

        public void OnSeekingStarted(object sender)
        {
            Control?.RaiseSeekingStartedEvent();
        }
    }
}
