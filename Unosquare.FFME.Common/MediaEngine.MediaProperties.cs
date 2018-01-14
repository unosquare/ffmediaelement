namespace Unosquare.FFME
{
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public partial class MediaEngine
    {
        #region Property Backing

        private readonly ReadOnlyDictionary<string, string> EmptyDictionary
            = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        private ulong? m_BufferBytesPerSecond = default(ulong?);
        private bool m_HasMediaEnded = false;
        private double m_BufferingProgress = 0;
        private int m_BufferCacheLength = 0;
        private double m_DownloadProgress = 0;
        private int m_DownloadCacheLength = 0;
        private string m_VideoSmtpeTimecode = string.Empty;
        private string m_VideoHardwareDecoder = string.Empty;
        private bool m_IsBuffering = false;
        private MediaEngineState m_CoreMediaState = MediaEngineState.Close;
        private bool m_IsOpening = false;
        private AtomicBoolean m_IsSeeking = new AtomicBoolean(false);

        #endregion

        #region Notification Properties

        /// <summary>
        /// Provides key-value pairs of the metadata contained in the media.
        /// Returns null when media has not been loaded.
        /// </summary>
        public ReadOnlyDictionary<string, string> Metadata => Container.Metadata ?? EmptyDictionary;

        /// <summary>
        /// Gets the media format. Returns null when media has not been loaded.
        /// </summary>
        public string MediaFormat => Container?.MediaFormatName;

        /// <summary>
        /// Provides stream, chapter and program info of the underlying media.
        /// Returns null when no media is loaded.
        /// </summary>
        public MediaInfo MediaInfo => Container?.MediaInfo;

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
        public bool HasAudio => Container?.Components.HasAudio ?? false;

        /// <summary> 
        /// Returns whether the given media has video. Only valid after the
        /// MediaOpened event has fired.
        /// </summary>
        public bool HasVideo => Container?.Components.HasVideo ?? false;

        /// <summary>
        /// Gets the video codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string VideoCodec => Container?.Components?.Video?.CodecName;

        /// <summary>
        /// Gets the video bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int VideoBitrate => Container?.Components?.Video?.Bitrate ?? 0;

        /// <summary>
        /// Returns the natural width of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary> 
        public int NaturalVideoWidth => Container?.Components?.Video?.FrameWidth ?? 0;

        /// <summary> 
        /// Returns the natural height of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoHeight => Container?.Components.Video?.FrameHeight ?? 0;

        /// <summary>
        /// Gets the video frame rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameRate => Container?.Components.Video?.BaseFrameRate ?? 0;

        /// <summary>
        /// Gets the duration in seconds of the video frame.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameLength => 1d / (Container?.Components?.Video?.BaseFrameRate ?? 0);

        /// <summary>
        /// Gets the name of the video hardware decoder in use.
        /// Enabling hardware acceleration does not guarantee decoding will be performed in hardware.
        /// When hardware decoding of frames is in use this will return the name of the HW accelerator.
        /// Otherwise it will return an empty string.
        /// </summary>
        public string VideoHardwareDecoder
        {
            get => m_VideoHardwareDecoder;
            internal set => SetProperty(ref m_VideoHardwareDecoder, value);
        }

        /// <summary>
        /// Gets the audio codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string AudioCodec => Container?.Components?.Audio?.CodecName;

        /// <summary>
        /// Gets the audio bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitrate => Container?.Components?.Audio?.Bitrate ?? 0;

        /// <summary>
        /// Gets the audio channels count.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioChannels => Container?.Components?.Audio?.Channels ?? 0;

        /// <summary>
        /// Gets the audio sample rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioSampleRate => Container?.Components?.Audio?.SampleRate ?? 0;

        /// <summary>
        /// Gets the audio bits per sample.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitsPerSample => Container?.Components?.Audio?.BitsPerSample ?? 0;

        /// <summary>
        /// Gets the Media's natural duration
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public TimeSpan? NaturalDuration => Container?.MediaDuration;

        /// <summary>
        /// Returns whether the currently loaded media can be paused.
        /// This is only valid after the MediaOpened event has fired.
        /// Note that this property is computed based on wether the stream is detected to be a live stream.
        /// </summary>
        public bool CanPause => IsOpen ? !IsLiveStream : false;

        /// <summary>
        /// Returns whether the currently loaded media is live or realtime and does not have a set duration
        /// This is only valid after the MediaOpened event has fired.
        /// </summary>
        public bool IsLiveStream => IsOpen ? Container.IsStreamRealtime && Container.MediaDuration == TimeSpan.MinValue : false;

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        public bool IsPositionUpdating
        {
            get => m_IsPositionUpdating.Value;
            private set => m_IsPositionUpdating.Value = value;
        }

        /// <summary>
        /// Gets a value indicating whether the currently loaded media can be seeked.
        /// </summary>
        public bool IsSeekable => Container?.IsStreamSeekable ?? false;

        /// <summary>
        /// Gets a value indicating whether the media is playing.
        /// </summary>
        public bool IsPlaying => MediaState == MediaEngineState.Play;

        /// <summary>
        /// Gets a value indicating whether the media has reached its end.
        /// </summary>
        public bool HasMediaEnded
        {
            get => m_HasMediaEnded;
            internal set => SetProperty(ref m_HasMediaEnded, value);
        }

        /// <summary>
        /// Get a value indicating whether the media is buffering.
        /// </summary>
        public bool IsBuffering
        {
            get => m_IsBuffering;
            private set => SetProperty(ref m_IsBuffering, value);
        }

        /// <summary>
        /// Gets a value indicating whether the media seeking is in progress.
        /// </summary>
        public bool IsSeeking
        {
            get => m_IsSeeking.Value == true;

            internal set
            {
                // Can't use SetProperty because the backing field is an AtomicBoolean
                if (m_IsSeeking.Value == value) return;
                m_IsSeeking.Value = value;
                SendOnPropertyChanged(nameof(IsSeeking));
            }
        }

        /// <summary>
        /// Returns the current video SMTPE timecode if available.
        /// If not available, this property returns an empty string.
        /// </summary>
        public string VideoSmtpeTimecode
        {
            get => m_VideoSmtpeTimecode;
            internal set => SetProperty(ref m_VideoSmtpeTimecode, value);
        }

        /// <summary>
        /// Gets the guessed buffered bytes in the packet queue per second.
        /// If bitrate information is available, then it returns the bitrate converted to byte rate.
        /// Returns null if it has not been guessed.
        /// </summary>
        public ulong? BufferBytesPerSecond
        {
            get => m_BufferBytesPerSecond;
            internal set => SetProperty(ref m_BufferBytesPerSecond, value);
        }

        /// <summary>
        /// Gets a value that indicates the percentage of buffering progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double BufferingProgress
        {
            get => m_BufferingProgress;
            internal set => SetProperty(ref m_BufferingProgress, value);
        }

        /// <summary>
        /// The packet buffer length.
        /// It is adjusted to 1 second if bitrate information is available.
        /// Otherwise, it's simply 512KB and it is guessed later on.
        /// </summary>
        public int BufferCacheLength
        {
            get => m_BufferCacheLength;
            internal set => SetProperty(ref m_BufferCacheLength, value);
        }

        /// <summary>
        /// Gets a value that indicates the percentage of download progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double DownloadProgress
        {
            get => m_DownloadProgress;
            internal set => SetProperty(ref m_DownloadProgress, value);
        }

        /// <summary>
        /// Gets the maximum packet buffer length, according to the bitrate (if available).
        /// If it's a realtime stream it will return 30 times the buffer cache length.
        /// Otherwise, it will return  4 times of the buffer cache length.
        /// </summary>
        public int DownloadCacheLength
        {
            get => m_DownloadCacheLength;
            internal set => SetProperty(ref m_DownloadCacheLength, value);
        }

        /// <summary>
        /// Gets a value indicating whether the media is in the process of opening.
        /// </summary>
        public bool IsOpening
        {
            get => m_IsOpening;
            internal set => SetProperty(ref m_IsOpening, value);
        }

        /// <summary>
        /// Gets a value indicating whether this media element
        /// currently has an open media url.
        /// </summary>
        public bool IsOpen => (IsOpening == false) && (Container?.IsOpen ?? false);

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        public MediaEngineState MediaState
        {
            get => m_CoreMediaState;

            internal set
            {
                SetProperty(ref m_CoreMediaState, value);
                SendOnPropertyChanged(nameof(IsPlaying));
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Updates the media properties notifying that there are new values to be read from all of them.
        /// Call this method only when necessary because it creates a lot of events.
        /// </summary>
        internal void NotifyPropertyChanges()
        {
            SendOnPropertyChanged(nameof(IsOpen));
            SendOnPropertyChanged(nameof(MediaFormat));
            SendOnPropertyChanged(nameof(HasAudio));
            SendOnPropertyChanged(nameof(HasVideo));
            SendOnPropertyChanged(nameof(VideoCodec));
            SendOnPropertyChanged(nameof(VideoBitrate));
            SendOnPropertyChanged(nameof(NaturalVideoWidth));
            SendOnPropertyChanged(nameof(NaturalVideoHeight));
            SendOnPropertyChanged(nameof(VideoFrameRate));
            SendOnPropertyChanged(nameof(VideoFrameLength));
            SendOnPropertyChanged(nameof(VideoHardwareDecoder));
            SendOnPropertyChanged(nameof(AudioCodec));
            SendOnPropertyChanged(nameof(AudioBitrate));
            SendOnPropertyChanged(nameof(AudioChannels));
            SendOnPropertyChanged(nameof(AudioSampleRate));
            SendOnPropertyChanged(nameof(AudioBitsPerSample));
            SendOnPropertyChanged(nameof(NaturalDuration));
            SendOnPropertyChanged(nameof(CanPause));
            SendOnPropertyChanged(nameof(IsLiveStream));
            SendOnPropertyChanged(nameof(IsSeekable));
            SendOnPropertyChanged(nameof(BufferBytesPerSecond));
            SendOnPropertyChanged(nameof(BufferCacheLength));
            SendOnPropertyChanged(nameof(DownloadCacheLength));
            SendOnPropertyChanged(nameof(FrameStepDuration));
            SendOnPropertyChanged(nameof(Metadata));
        }

        /// <summary>
        /// Resets the controller properies.
        /// </summary>
        internal void ResetControllerProperties()
        {
            Volume = Defaults.DefaultVolume;
            Balance = Defaults.DefaultBalance;
            SpeedRatio = Defaults.DefaultSpeedRatio;
            IsMuted = false;
            VideoSmtpeTimecode = string.Empty;
            VideoHardwareDecoder = string.Empty;
            IsMuted = false;
            HasMediaEnded = false;
            UpdatePosition(TimeSpan.Zero);
        }

        #endregion
    }
}
