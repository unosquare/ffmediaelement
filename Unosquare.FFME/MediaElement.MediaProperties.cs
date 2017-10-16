﻿namespace Unosquare.FFME
{
    using Core;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;

    public partial class MediaElement
    {
        #region Property Backing

        private readonly ObservableCollection<KeyValuePair<string, string>> m_MetadataBase;
        private readonly ICollectionView m_Metadata;
        private bool m_HasMediaEnded = false;
        private double m_BufferingProgress = 0;
        private double m_DownloadProgress = 0;
        private string m_VideoSmtpeTimecode = string.Empty;
        private bool m_IsBuffering = false;
        private MediaState m_MediaState = MediaState.Close;
        private bool m_IsOpening = false;
        private AtomicBoolean m_IsSeeking = new AtomicBoolean();

        #endregion

        #region Notification Properties

        /// <summary>
        /// Provides key-value pairs of the metadata contained in the media.
        /// Returns null when media has not been loaded.
        /// </summary>
        public ICollectionView Metadata
        {
            get { return m_Metadata; }
        }

        /// <summary>
        /// Gets the media format. Returns null when media has not been loaded.
        /// </summary>
        public string MediaFormat
        {
            get { return Container?.MediaFormatName; }
        }

        /// <summary>
        /// Gets the duration of a single frame step.
        /// If there is a video component with a framerate, this propery returns the length of a frame.
        /// If there is no video component it simply returns a tenth of a second.
        /// </summary>
        public TimeSpan FrameStepDuration
        {
            get
            {
                if (IsOpen == false) { return TimeSpan.Zero; }

                if (HasVideo)
                {
                    if (VideoFrameLength > 0)
                        return TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * VideoFrameLength * 1000d, 0));
                }

                return TimeSpan.FromSeconds(0.1d);
            }
        }

        /// <summary> 
        /// Returns whether the given media has audio. 
        /// Only valid after the MediaOpened event has fired.
        /// </summary> 
        public bool HasAudio
        {
            get { return Container == null ? false : Container.Components.HasAudio; }
        }

        /// <summary> 
        /// Returns whether the given media has video. Only valid after the
        /// MediaOpened event has fired.
        /// </summary>
        public bool HasVideo
        {
            get { return Container?.Components.HasVideo ?? false; }
        }

        /// <summary>
        /// Gets the video codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string VideoCodec
        {
            get { return Container?.Components?.Video?.CodecName; }
        }

        /// <summary>
        /// Gets the video bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int VideoBitrate
        {
            get { return Container?.Components?.Video?.Bitrate ?? 0; }
        }

        /// <summary>
        /// Returns the natural width of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary> 
        public int NaturalVideoWidth
        {
            get { return Container?.Components?.Video?.FrameWidth ?? 0; }
        }

        /// <summary> 
        /// Returns the natural height of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoHeight
        {
            get { return Container?.Components.Video?.FrameHeight ?? 0; }
        }

        /// <summary>
        /// Gets the video frame rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameRate
        {
            get { return Container?.Components.Video?.BaseFrameRate ?? 0; }
        }

        /// <summary>
        /// Gets the duration in seconds of the video frame.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameLength
        {
            get { return 1d / (Container?.Components?.Video?.BaseFrameRate ?? 0); }
        }

        /// <summary>
        /// Gets the audio codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string AudioCodec
        {
            get { return Container?.Components?.Audio?.CodecName; }
        }

        /// <summary>
        /// Gets the audio bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitrate
        {
            get { return Container?.Components?.Audio?.Bitrate ?? 0; }
        }

        /// <summary>
        /// Gets the audio channels count.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioChannels
        {
            get { return Container?.Components?.Audio?.Channels ?? 0; }
        }

        /// <summary>
        /// Gets the audio sample rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioSampleRate
        {
            get { return Container?.Components?.Audio?.SampleRate ?? 0; }
        }

        /// <summary>
        /// Gets the audio bits per sample.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitsPerSample
        {
            get { return Container?.Components?.Audio?.BitsPerSample ?? 0; }
        }

        /// <summary>
        /// Gets the Media's natural duration
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public Duration NaturalDuration
        {
            get
            {
              return Container == null
                ? Duration.Automatic
                : (Container.MediaDuration == TimeSpan.MinValue
                  ? Duration.Forever
                  : (Container.MediaDuration < TimeSpan.Zero ? default(Duration) : new Duration(Container.MediaDuration)));
            }
        }

        /// <summary>
        /// Returns whether the currently loaded media can be paused.
        /// This is only valid after the MediaOpened event has fired.
        /// Note that this property is computed based on wether the stream is detected to be a live stream.
        /// </summary>
        public bool CanPause
        {
            get { return IsOpen ? !IsLiveStream : false; }
        }

        /// <summary>
        /// Returns whether the currently loaded media is live or realtime
        /// This is only valid after the MediaOpened event has fired.
        /// </summary>
        public bool IsLiveStream
        {
            get { return IsOpen ? Container.IsStreamRealtime && Container.MediaDuration == TimeSpan.MinValue: false; }
        }

        /// <summary>
        /// Gets a value indicating whether the currently loaded media can be seeked.
        /// </summary>
        public bool IsSeekable
        {
            get { return Container != null ? Container.IsStreamSeekable : false; }
        }

        /// <summary>
        /// Gets a value indicating whether the media is playing.
        /// </summary>
        public bool IsPlaying
        {
            get { return MediaState == MediaState.Play; }
        }

        /// <summary>
        /// Gets a value indicating whether the media has reached its end.
        /// </summary>
        public bool HasMediaEnded
        {
            get { return m_HasMediaEnded; }
            internal set { SetProperty(ref m_HasMediaEnded, value); }
        }

        /// <summary>
        /// Get a value indicating whether the media is buffering.
        /// </summary>
        public bool IsBuffering
        {
            get { return m_IsBuffering; }
            private set { SetProperty(ref m_IsBuffering, value); }
        }

        /// <summary>
        /// Gets a value indicating whether the media seeking is in progress.
        /// </summary>
        public bool IsSeeking
        {
            get
            {
                return m_IsSeeking.Value == true;
            }

            internal set
            {
                if (m_IsSeeking.Value == value) return;
                m_IsSeeking.Value = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Returns the current video SMTPE timecode if available.
        /// If not available, this property returns an empty string.
        /// </summary>
        public string VideoSmtpeTimecode
        {
            get { return m_VideoSmtpeTimecode; }
            internal set { SetProperty(ref m_VideoSmtpeTimecode, value); }
        }

        /// <summary>
        /// Gets a value that indicates the percentage of buffering progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double BufferingProgress
        {
            get { return m_BufferingProgress; }
            private set { SetProperty(ref m_BufferingProgress, value); }
        }

        /// <summary>
        /// The wait packet buffer length.
        /// It is adjusted to 1 second if bitrate information is available.
        /// Otherwise, it's simply 512KB
        /// </summary>
        public int BufferCacheLength
        {
            get
            {
                if (Container == null || (HasVideo && VideoBitrate <= 0) || (HasAudio && AudioBitrate <= 0))
                {
                    return 512 * 1024; // 512 kilobytes
                }
                else
                {
                    var byteRate = (VideoBitrate + AudioBitrate) / 8;
                    return (Container?.IsStreamRealtime ?? false) ?
                        byteRate / 2 : byteRate;
                }
            }
        }

        /// <summary>
        /// Gets a value that indicates the percentage of download progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double DownloadProgress
        {
            get { return m_DownloadProgress; }
            private set { SetProperty(ref m_DownloadProgress, value); }
        }

        /// <summary>
        /// Gets the maximum packet buffer length, according to the bitrate (if available).
        /// If it's a realtime stream it will return 30 times the buffer cache length.
        /// Otherwise, it will return  4 times of the buffer cache length.
        /// </summary>
        public int DownloadCacheLength
        {
            get
            {
                return (Container?.IsStreamRealtime ?? false) ?
                    BufferCacheLength * 30 : BufferCacheLength * 4;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the media is in the process of opening.
        /// </summary>
        public bool IsOpening
        {
            get { return m_IsOpening; }
            internal set { SetProperty(ref m_IsOpening, value); }
        }

        /// <summary>
        /// Gets a value indicating whether this media element
        /// currently has an open media url.
        /// </summary>
        public bool IsOpen
        {
            get { return (IsOpening == false) && (Container?.IsOpen ?? false); }
        }

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        public MediaState MediaState
        {
            get
            {
                return m_MediaState;
            }

            internal set
            {
                SetProperty(ref m_MediaState, value);
                OnPropertyChanged(nameof(IsPlaying));
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Updates the metada property.
        /// </summary>
        internal void UpdateMetadaProperty()
        {
            m_MetadataBase.Clear();
            if (Container != null && Container.Metadata != null)
            {
                foreach (var kvp in Container.Metadata)
                    m_MetadataBase.Add(kvp);
            }

            OnPropertyChanged(nameof(Metadata));
        }

        /// <summary>
        /// Updates the media properties notifying that there are new values to be read from all of them.
        /// Call this method only when necessary because it creates a lot of events.
        /// </summary>
        internal void NotifyPropertyChanges()
        {
            UpdateMetadaProperty();

            OnPropertyChanged(nameof(IsOpen));
            OnPropertyChanged(nameof(MediaFormat));
            OnPropertyChanged(nameof(HasAudio));
            OnPropertyChanged(nameof(HasVideo));
            OnPropertyChanged(nameof(VideoCodec));
            OnPropertyChanged(nameof(VideoBitrate));
            OnPropertyChanged(nameof(NaturalVideoWidth));
            OnPropertyChanged(nameof(NaturalVideoHeight));
            OnPropertyChanged(nameof(VideoFrameRate));
            OnPropertyChanged(nameof(VideoFrameLength));
            OnPropertyChanged(nameof(AudioCodec));
            OnPropertyChanged(nameof(AudioBitrate));
            OnPropertyChanged(nameof(AudioChannels));
            OnPropertyChanged(nameof(AudioSampleRate));
            OnPropertyChanged(nameof(AudioBitsPerSample));
            OnPropertyChanged(nameof(NaturalDuration));
            OnPropertyChanged(nameof(CanPause));
            OnPropertyChanged(nameof(IsLiveStream));
            OnPropertyChanged(nameof(IsSeekable));
            OnPropertyChanged(nameof(BufferCacheLength));
            OnPropertyChanged(nameof(DownloadCacheLength));
            OnPropertyChanged(nameof(FrameStepDuration));
        }

        /// <summary>
        /// Resets the dependency properies.
        /// </summary>
        internal void ResetDependencyProperies()
        {
            Volume = Constants.DefaultVolume;
            Balance = Constants.DefaultBalance;
            SpeedRatio = Constants.DefaultSpeedRatio;
            IsMuted = false;
            DownloadProgress = 0;
            BufferingProgress = 0;
            VideoSmtpeTimecode = string.Empty;
            IsBuffering = false;
            IsMuted = false;
            HasMediaEnded = false;
            UpdatePosition(TimeSpan.Zero);
        }

        #endregion
    }
}
