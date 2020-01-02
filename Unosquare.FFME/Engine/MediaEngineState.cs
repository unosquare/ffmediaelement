namespace Unosquare.FFME.Engine
{
    using Common;
    using Container;
    using Primitives;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Contains all the status properties of the stream being handled by the media engine.
    /// </summary>
    internal sealed class MediaEngineState : ViewModelBase, IMediaEngineState
    {
        #region Property Backing and Private State

        private static readonly IReadOnlyDictionary<string, string> EmptyDictionary = new Dictionary<string, string>(0);

        private readonly MediaEngine MediaCore;
        private readonly AtomicInteger m_MediaState = new AtomicInteger((int)MediaPlaybackState.Close);
        private readonly AtomicBoolean m_HasMediaEnded = new AtomicBoolean(default);

        private readonly AtomicBoolean m_IsBuffering = new AtomicBoolean(default);
        private readonly AtomicLong m_DecodingBitRate = new AtomicLong(default);
        private readonly AtomicDouble m_BufferingProgress = new AtomicDouble(default);
        private readonly AtomicDouble m_DownloadProgress = new AtomicDouble(default);
        private readonly AtomicLong m_PacketBufferLength = new AtomicLong(default);
        private readonly AtomicTimeSpan m_PacketBufferDuration = new AtomicTimeSpan(TimeSpan.MinValue);
        private readonly AtomicInteger m_PacketBufferCount = new AtomicInteger(default);

        private readonly AtomicTimeSpan m_FramePosition = new AtomicTimeSpan(default);
        private readonly AtomicTimeSpan m_Position = new AtomicTimeSpan(default);
        private readonly AtomicDouble m_SpeedRatio = new AtomicDouble(Constants.DefaultSpeedRatio);
        private readonly AtomicDouble m_Volume = new AtomicDouble(Constants.DefaultVolume);
        private readonly AtomicDouble m_Balance = new AtomicDouble(Constants.DefaultBalance);
        private readonly AtomicBoolean m_IsMuted = new AtomicBoolean(false);
        private readonly AtomicBoolean m_ScrubbingEnabled = new AtomicBoolean(true);
        private readonly AtomicBoolean m_VerticalSyncEnabled = new AtomicBoolean(true);

        private Uri m_Source;
        private bool m_IsOpen;
        private TimeSpan m_PositionStep;
        private long m_BitRate;
        private IReadOnlyDictionary<string, string> m_Metadata = EmptyDictionary;
        private bool m_CanPause;
        private string m_MediaFormat;
        private long m_MediaStreamSize;
        private int m_VideoStreamIndex;
        private int m_AudioStreamIndex;
        private int m_SubtitleStreamIndex;
        private bool m_HasAudio;
        private bool m_HasVideo;
        private bool m_HasSubtitles;
        private string m_VideoCodec;
        private long m_VideoBitRate;
        private double m_VideoRotation;
        private int m_NaturalVideoWidth;
        private int m_NaturalVideoHeight;
        private string m_VideoAspectRatio;
        private double m_VideoFrameRate;
        private string m_AudioCodec;
        private long m_AudioBitRate;
        private int m_AudioChannels;
        private int m_AudioSampleRate;
        private int m_AudioBitsPerSample;
        private TimeSpan? m_NaturalDuration;
        private TimeSpan? m_PlaybackStartTime;
        private TimeSpan? m_PlaybackEndTime;
        private bool m_IsLiveStream;
        private bool m_IsNetworkStream;
        private bool m_IsSeekable;

        private string m_VideoSmtpeTimeCode = string.Empty;
        private string m_VideoHardwareDecoder = string.Empty;
        private bool m_HasClosedCaptions;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaEngineState" /> class.
        /// </summary>
        /// <param name="mediaCore">The associated media core.</param>
        internal MediaEngineState(MediaEngine mediaCore)
            : base(false)
        {
            MediaCore = mediaCore;
            ResetAll();
        }

        #endregion

        #region Controller Properties

        /// <inheritdoc />
        public Uri Source
        {
            get => m_Source;
            private set => SetProperty(ref m_Source, value);
        }

        /// <inheritdoc />
        public double SpeedRatio
        {
            get => m_SpeedRatio.Value;
            set => SetProperty(m_SpeedRatio, value.Clamp(Constants.MinSpeedRatio, Constants.MaxSpeedRatio));
        }

        /// <inheritdoc />
        public double Volume
        {
            get => m_Volume.Value;
            set => SetProperty(m_Volume, value.Clamp(Constants.MinVolume, Constants.MaxVolume));
        }

        /// <inheritdoc />
        public double Balance
        {
            get => m_Balance.Value;
            set => SetProperty(m_Balance, value.Clamp(Constants.MinBalance, Constants.MaxBalance));
        }

        /// <inheritdoc />
        public bool IsMuted
        {
            get => m_IsMuted.Value;
            set => SetProperty(m_IsMuted, value);
        }

        /// <inheritdoc />
        public bool ScrubbingEnabled
        {
            get => m_ScrubbingEnabled.Value;
            set => SetProperty(m_ScrubbingEnabled, value);
        }

        /// <inheritdoc />
        public bool VerticalSyncEnabled
        {
            get => m_VerticalSyncEnabled.Value;
            set => SetProperty(m_VerticalSyncEnabled, value);
        }

        #endregion

        #region Renderer Update Driven Properties

        /// <inheritdoc />
        public MediaPlaybackState MediaState
        {
            get => (MediaPlaybackState)m_MediaState.Value;
            internal set
            {
                var oldState = (MediaPlaybackState)m_MediaState.Value;
                if (!SetProperty(m_MediaState, (int)value))
                    return;

                ReportCommandStatus();
                ReportTimingStatus();
                MediaCore.SendOnMediaStateChanged(oldState, value);
            }
        }

        /// <inheritdoc />
        public TimeSpan Position
        {
            get => m_Position.Value;
            private set => SetProperty(m_Position, value);
        }

        /// <inheritdoc />
        public TimeSpan FramePosition
        {
            get => m_FramePosition.Value;
            private set => SetProperty(m_FramePosition, value);
        }

        /// <inheritdoc />
        public bool HasMediaEnded
        {
            get => m_HasMediaEnded.Value;
            internal set
            {
                if (!SetProperty(m_HasMediaEnded, value))
                    return;

                if (value) MediaCore.SendOnMediaEnded();
            }
        }

        /// <inheritdoc />
        public string VideoSmtpeTimeCode
        {
            get => m_VideoSmtpeTimeCode;
            private set => SetProperty(ref m_VideoSmtpeTimeCode, value);
        }

        /// <inheritdoc />
        public string VideoHardwareDecoder
        {
            get => m_VideoHardwareDecoder;
            private set => SetProperty(ref m_VideoHardwareDecoder, value);
        }

        /// <inheritdoc />
        public bool HasClosedCaptions
        {
            get => m_HasClosedCaptions;
            private set => SetProperty(ref m_HasClosedCaptions, value);
        }

        #endregion

        #region Self-Updating Properties

        /// <inheritdoc />
        public bool IsAtEndOfStream => MediaCore.Container?.IsAtEndOfStream ?? false;

        /// <inheritdoc />
        public bool IsPlaying => IsOpen && MediaCore.Timing.IsRunning;

        /// <inheritdoc />
        public bool IsPaused => IsOpen && !MediaCore.Timing.IsRunning;

        /// <inheritdoc />
        public bool IsSeeking => MediaCore.Commands?.IsSeeking ?? false;

        /// <inheritdoc />
        public bool IsClosing => MediaCore.Commands?.IsClosing ?? false;

        /// <inheritdoc />
        public bool IsOpening => MediaCore.Commands?.IsOpening ?? false;

        /// <inheritdoc />
        public bool IsChanging => MediaCore.Commands?.IsChanging ?? false;

        #endregion

        #region Container Fixed, One-Time Properties

        /// <inheritdoc />
        public bool IsOpen
        {
            get => m_IsOpen;
            private set
            {
                SetProperty(ref m_IsOpen, value);
                ReportTimingStatus();
            }
        }

        /// <inheritdoc />
        public TimeSpan PositionStep
        {
            get => m_PositionStep;
            private set => SetProperty(ref m_PositionStep, value);
        }

        /// <inheritdoc />
        public long BitRate
        {
            get => m_BitRate;
            private set => SetProperty(ref m_BitRate, value);
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> Metadata
        {
            get => m_Metadata;
            private set => SetProperty(ref m_Metadata, value);
        }

        /// <inheritdoc />
        public bool CanPause
        {
            get => m_CanPause;
            private set => SetProperty(ref m_CanPause, value);
        }

        /// <inheritdoc />
        public string MediaFormat
        {
            get => m_MediaFormat;
            private set => SetProperty(ref m_MediaFormat, value);
        }

        /// <inheritdoc />
        public long MediaStreamSize
        {
            get => m_MediaStreamSize;
            private set => SetProperty(ref m_MediaStreamSize, value);
        }

        /// <inheritdoc />
        public int VideoStreamIndex
        {
            get => m_VideoStreamIndex;
            private set => SetProperty(ref m_VideoStreamIndex, value);
        }

        /// <inheritdoc />
        public int AudioStreamIndex
        {
            get => m_AudioStreamIndex;
            private set => SetProperty(ref m_AudioStreamIndex, value);
        }

        /// <inheritdoc />
        public int SubtitleStreamIndex
        {
            get => m_SubtitleStreamIndex;
            private set => SetProperty(ref m_SubtitleStreamIndex, value);
        }

        /// <inheritdoc />
        public bool HasAudio
        {
            get => m_HasAudio;
            private set => SetProperty(ref m_HasAudio, value);
        }

        /// <inheritdoc />
        public bool HasVideo
        {
            get => m_HasVideo;
            private set => SetProperty(ref m_HasVideo, value);
        }

        /// <inheritdoc />
        public bool HasSubtitles
        {
            get => m_HasSubtitles;
            private set => SetProperty(ref m_HasSubtitles, value);
        }

        /// <inheritdoc />
        public string VideoCodec
        {
            get => m_VideoCodec;
            private set => SetProperty(ref m_VideoCodec, value);
        }

        /// <inheritdoc />
        public long VideoBitRate
        {
            get => m_VideoBitRate;
            private set => SetProperty(ref m_VideoBitRate, value);
        }

        /// <inheritdoc />
        public double VideoRotation
        {
            get => m_VideoRotation;
            private set => SetProperty(ref m_VideoRotation, value);
        }

        /// <inheritdoc />
        public int NaturalVideoWidth
        {
            get => m_NaturalVideoWidth;
            private set => SetProperty(ref m_NaturalVideoWidth, value);
        }

        /// <inheritdoc />
        public int NaturalVideoHeight
        {
            get => m_NaturalVideoHeight;
            private set => SetProperty(ref m_NaturalVideoHeight, value);
        }

        /// <inheritdoc />
        public string VideoAspectRatio
        {
            get => m_VideoAspectRatio;
            private set => SetProperty(ref m_VideoAspectRatio, value);
        }

        /// <inheritdoc />
        public double VideoFrameRate
        {
            get => m_VideoFrameRate;
            private set => SetProperty(ref m_VideoFrameRate, value);
        }

        /// <inheritdoc />
        public string AudioCodec
        {
            get => m_AudioCodec;
            private set => SetProperty(ref m_AudioCodec, value);
        }

        /// <inheritdoc />
        public long AudioBitRate
        {
            get => m_AudioBitRate;
            private set => SetProperty(ref m_AudioBitRate, value);
        }

        /// <inheritdoc />
        public int AudioChannels
        {
            get => m_AudioChannels;
            private set => SetProperty(ref m_AudioChannels, value);
        }

        /// <inheritdoc />
        public int AudioSampleRate
        {
            get => m_AudioSampleRate;
            private set => SetProperty(ref m_AudioSampleRate, value);
        }

        /// <inheritdoc />
        public int AudioBitsPerSample
        {
            get => m_AudioBitsPerSample;
            private set => SetProperty(ref m_AudioBitsPerSample, value);
        }

        /// <inheritdoc />
        public TimeSpan? NaturalDuration
        {
            get => m_NaturalDuration;
            private set => SetProperty(ref m_NaturalDuration, value);
        }

        /// <inheritdoc />
        public TimeSpan? PlaybackStartTime
        {
            get => m_PlaybackStartTime;
            private set => SetProperty(ref m_PlaybackStartTime, value);
        }

        /// <inheritdoc />
        public TimeSpan? PlaybackEndTime
        {
            get => m_PlaybackEndTime;
            private set => SetProperty(ref m_PlaybackEndTime, value);
        }

        /// <inheritdoc />
        public bool IsLiveStream
        {
            get => m_IsLiveStream;
            private set => SetProperty(ref m_IsLiveStream, value);
        }

        /// <inheritdoc />
        public bool IsNetworkStream
        {
            get => m_IsNetworkStream;
            private set => SetProperty(ref m_IsNetworkStream, value);
        }

        /// <inheritdoc />
        public bool IsSeekable
        {
            get => m_IsSeekable;
            private set => SetProperty(ref m_IsSeekable, value);
        }

        #endregion

        #region State Method Managed Media Properties

        /// <inheritdoc />
        public bool IsBuffering
        {
            get => m_IsBuffering.Value;
            private set => SetProperty(m_IsBuffering, value);
        }

        /// <inheritdoc />
        public long DecodingBitRate
        {
            get => m_DecodingBitRate.Value;
            private set => SetProperty(m_DecodingBitRate, value);
        }

        /// <inheritdoc />
        public double BufferingProgress
        {
            get => m_BufferingProgress.Value;
            private set => SetProperty(m_BufferingProgress, value);
        }

        /// <inheritdoc />
        public double DownloadProgress
        {
            get => m_DownloadProgress.Value;
            private set => SetProperty(m_DownloadProgress, value);
        }

        /// <inheritdoc />
        public long PacketBufferLength
        {
            get => m_PacketBufferLength.Value;
            private set => SetProperty(m_PacketBufferLength, value);
        }

        /// <inheritdoc />
        public TimeSpan PacketBufferDuration
        {
            get => m_PacketBufferDuration.Value;
            private set => SetProperty(m_PacketBufferDuration, value);
        }

        /// <inheritdoc />
        public int PacketBufferCount
        {
            get => m_PacketBufferCount.Value;
            private set
            {
                SetProperty(m_PacketBufferCount, value);
                NotifyPropertyChanged(nameof(IsAtEndOfStream));
            }
        }

        #endregion

        #region State Management Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReportCommandStatus() => NotifyPropertyChanged(nameof(IsSeeking), nameof(IsClosing), nameof(IsOpening), nameof(IsChanging));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReportTimingStatus() => NotifyPropertyChanged(nameof(IsPlaying), nameof(IsPaused));

        /// <summary>
        /// Updates the <see cref="Source"/> property.
        /// </summary>
        /// <param name="newSource">The new source.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateSource(Uri newSource) => Source = newSource;

        /// <summary>
        /// Updates the fixed container properties.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateFixedContainerProperties()
        {
            BitRate = MediaCore.Container?.MediaBitRate ?? default;
            IsOpen = !IsOpening && (MediaCore.Container?.IsOpen ?? default);
            Metadata = MediaCore.Container?.Metadata ?? EmptyDictionary;
            MediaFormat = MediaCore.Container?.MediaFormatName;
            MediaStreamSize = MediaCore.Container?.MediaStreamSize ?? default;
            VideoStreamIndex = MediaCore.Container?.Components.Video?.StreamIndex ?? -1;
            AudioStreamIndex = MediaCore.Container?.Components.Audio?.StreamIndex ?? -1;
            SubtitleStreamIndex = MediaCore.Container?.Components.Subtitles?.StreamIndex ?? -1;
            HasAudio = MediaCore.Container?.Components.HasAudio ?? default;
            HasVideo = MediaCore.Container?.Components.HasVideo ?? default;
            HasClosedCaptions = MediaCore.Container?.Components.Video?.StreamInfo?.HasClosedCaptions ?? default;
            HasSubtitles = (MediaCore.PreloadedSubtitles?.Count ?? 0) > 0
                || (MediaCore.Container?.Components.HasSubtitles ?? false);
            VideoCodec = MediaCore.Container?.Components.Video?.CodecName;
            VideoBitRate = MediaCore.Container?.Components.Video?.BitRate ?? default;
            VideoRotation = MediaCore.Container?.Components.Video?.DisplayRotation ?? default;
            NaturalVideoWidth = MediaCore.Container?.Components.Video?.FrameWidth ?? default;
            NaturalVideoHeight = MediaCore.Container?.Components.Video?.FrameHeight ?? default;
            VideoFrameRate = MediaCore.Container?.Components.Video?.AverageFrameRate ?? default;
            AudioCodec = MediaCore.Container?.Components.Audio?.CodecName;
            AudioBitRate = MediaCore.Container?.Components.Audio?.BitRate ?? default;
            AudioChannels = MediaCore.Container?.Components.Audio?.Channels ?? default;
            AudioSampleRate = MediaCore.Container?.Components.Audio?.SampleRate ?? default;
            AudioBitsPerSample = MediaCore.Container?.Components.Audio?.BitsPerSample ?? default;
            NaturalDuration = MediaCore.Timing?.Duration;
            PlaybackStartTime = MediaCore.Timing?.StartTime;
            PlaybackEndTime = MediaCore.Timing?.EndTime;
            IsLiveStream = MediaCore.Container?.IsLiveStream ?? default;
            IsNetworkStream = MediaCore.Container?.IsNetworkStream ?? default;
            IsSeekable = MediaCore.Container?.IsStreamSeekable ?? default;
            CanPause = IsOpen ? !IsLiveStream : default;

            var videoAspectWidth = MediaCore.Container?.Components.Video?.DisplayAspectWidth ?? default;
            var videoAspectHeight = MediaCore.Container?.Components.Video?.DisplayAspectHeight ?? default;
            VideoAspectRatio = videoAspectWidth != default && videoAspectHeight != default ?
                $"{videoAspectWidth}:{videoAspectHeight}" : default;

            var mediaType = MediaCore.Container?.Components.MainMediaType ?? MediaType.None;
            var main = MediaCore.Container?.Components.Main;

            switch (mediaType)
            {
                case MediaType.Audio:
                    PositionStep = TimeSpan.FromTicks(Convert.ToInt64(
                        TimeSpan.TicksPerMillisecond * AudioSampleRate / 1000d));
                    break;

                case MediaType.Video:
                    var baseFrameRate = (main as VideoComponent)?.BaseFrameRate ?? 1d;
                    PositionStep = TimeSpan.FromTicks(Convert.ToInt64(
                        TimeSpan.TicksPerMillisecond * 1000d / baseFrameRate));
                    break;

                default:
                    PositionStep = default;
                    break;
            }
        }

        /// <summary>
        /// Updates state properties coming from a new media block.
        /// </summary>
        /// <param name="block">The block.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateDynamicBlockProperties(MediaBlock block)
        {
            if (block == null) return;

            // Update the discrete frame position upon rendering
            if (block.MediaType == (MediaCore.Container?.Components.MainMediaType ?? MediaType.None))
                FramePosition = block.StartTime;

            // Update video block properties
            if (block is VideoBlock == false) return;

            // Capture the video block
            var videoBlock = (VideoBlock)block;

            // I don't know of any codecs changing the width and the height dynamically
            // but we update the properties just to be safe.
            NaturalVideoWidth = videoBlock.PixelWidth;
            NaturalVideoHeight = videoBlock.PixelHeight;

            // Update the has closed captions state as it might come in later
            // as frames are decoded
            if (HasClosedCaptions == false && videoBlock.ClosedCaptions.Count > 0)
                HasClosedCaptions = true;

            VideoSmtpeTimeCode = videoBlock.SmtpeTimeCode;
            VideoHardwareDecoder = videoBlock.IsHardwareFrame ?
                videoBlock.HardwareAcceleratorName : string.Empty;
        }

        /// <summary>
        /// Updates the playback position and related properties.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReportPlaybackPosition() => ReportPlaybackPosition(MediaCore.PlaybackPosition);

        /// <summary>
        /// Updates the playback position related properties.
        /// </summary>
        /// <param name="newPosition">The new playback position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReportPlaybackPosition(TimeSpan newPosition)
        {
            var oldSpeedRatio = MediaCore.Timing.SpeedRatio;
            var newSpeedRatio = SpeedRatio;

            if (Math.Abs(oldSpeedRatio - newSpeedRatio) > double.Epsilon)
                MediaCore.Timing.SpeedRatio = SpeedRatio;

            var oldPosition = Position;
            if (oldPosition.Ticks == newPosition.Ticks)
                return;

            Position = newPosition;
            MediaCore.SendOnPositionChanged(oldPosition, newPosition);
        }

        /// <summary>
        /// Resets all media state properties.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResetAll()
        {
            ResetMediaProperties();
            UpdateFixedContainerProperties();
            InitializeBufferingStatistics();
            ReportCommandStatus();
            ReportTimingStatus();
        }

        /// <summary>
        /// Resets the controller properties.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResetMediaProperties()
        {
            // Reset Method-controlled properties
            Position = default;
            FramePosition = default;
            HasMediaEnded = default;

            // Reset decoder and buffering
            ResetBufferingStatistics();

            VideoSmtpeTimeCode = string.Empty;
            VideoHardwareDecoder = string.Empty;

            // Reset controller properties
            SpeedRatio = Constants.DefaultSpeedRatio;

            MediaState = MediaPlaybackState.Close;
        }

        /// <summary>
        /// Resets all the buffering properties to their defaults.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InitializeBufferingStatistics()
        {
            const long MinimumValidFileSize = 1024 * 1024; // 1 MB

            // Start with default values
            ResetBufferingStatistics();

            // Reset the properties if the is no associated container
            if (MediaCore.Container == null)
            {
                MediaStreamSize = default;
                return;
            }

            // Try to get a valid stream size
            MediaStreamSize = MediaCore.Container.MediaStreamSize;
            var durationSeconds = NaturalDuration?.TotalSeconds ?? 0d;

            // Compute the bit rate and buffering properties based on media byte size
            if (MediaStreamSize >= MinimumValidFileSize && IsSeekable && durationSeconds > 0)
            {
                // The bit rate is simply the media size over the total duration
                BitRate = Convert.ToInt64(8d * MediaStreamSize / durationSeconds);
            }
        }

        /// <summary>
        /// Updates the decoding bit rate and duration of the reference timing component.
        /// </summary>
        /// <param name="bitRate">The bit rate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateDecodingStats(long bitRate)
        {
            DecodingBitRate = bitRate;
            NaturalDuration = MediaCore.Timing?.Duration;
            PlaybackStartTime = MediaCore.Timing?.StartTime;
            PlaybackEndTime = MediaCore.Timing?.EndTime;
        }

        /// <summary>
        /// Updates the buffering properties: <see cref="PacketBufferCount" />, <see cref="PacketBufferLength" />,
        /// <see cref="IsBuffering" />, <see cref="BufferingProgress" />, <see cref="DownloadProgress" />.
        /// If a change is detected on the <see cref="IsBuffering" /> property then a notification is sent.
        /// </summary>
        /// <param name="bufferLength">Length of the packet buffer.</param>
        /// <param name="bufferCount">The packet buffer count.</param>
        /// <param name="bufferCountMax">The packet buffer count maximum for all components.</param>
        /// <param name="bufferDuration">Duration of the packet buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateBufferingStats(long bufferLength, int bufferCount, int bufferCountMax, TimeSpan bufferDuration)
        {
            PacketBufferCount = bufferCount;
            PacketBufferLength = bufferLength;
            PacketBufferDuration = bufferDuration;
            BufferingProgress = bufferCountMax <= 0 ? 0 : Math.Min(1d, (double)bufferCount / bufferCountMax);
            DownloadProgress = Math.Min(1d, (double)bufferLength / MediaEngine.BufferLengthMax);

            // Check if we are currently buffering
            var isCurrentlyBuffering = MediaCore.ShouldReadMorePackets
                && (MediaCore.IsSyncBuffering || BufferingProgress < 1d);

            // Detect and notify a change in buffering state
            if (isCurrentlyBuffering == IsBuffering)
                return;

            IsBuffering = isCurrentlyBuffering;
            if (isCurrentlyBuffering)
                MediaCore.SendOnBufferingStarted();
            else
                MediaCore.SendOnBufferingEnded();
        }

        /// <summary>
        /// Resets the buffering statistics.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetBufferingStatistics()
        {
            IsBuffering = default;
            DecodingBitRate = default;
            BufferingProgress = default;
            DownloadProgress = default;
            PacketBufferLength = default;
            PacketBufferDuration = TimeSpan.MinValue;
            PacketBufferCount = default;
        }

        #endregion
    }
}
