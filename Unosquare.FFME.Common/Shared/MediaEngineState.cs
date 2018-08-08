﻿namespace Unosquare.FFME.Shared
{
    using Decoding;
    using Primitives;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Contains all the status properties of the stream being handled by the media engine.
    /// </summary>
    public sealed class MediaEngineState : IMediaEngineState
    {
        #region Property Backing and Private State

        private static readonly TimeSpan GenericFrameStepDuration = TimeSpan.FromSeconds(0.01d);
        private static readonly ReadOnlyDictionary<string, string> EmptyDictionary
            = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        private readonly MediaEngine MediaCore = null;
        private readonly AtomicInteger m_MediaState = new AtomicInteger((int)PlaybackStatus.Close);
        private readonly AtomicBoolean m_HasMediaEnded = new AtomicBoolean(default);

        private readonly AtomicBoolean m_IsBuffering = new AtomicBoolean(default);
        private readonly AtomicLong m_DecodingBitrate = new AtomicLong(default);
        private readonly AtomicDouble m_BufferingProgress = new AtomicDouble(default);
        private readonly AtomicDouble m_DownloadProgress = new AtomicDouble(default);
        private readonly AtomicLong m_PacketBufferLength = new AtomicLong(default);
        private readonly AtomicInteger m_PacketBufferCount = new AtomicInteger(default);

        private readonly AtomicTimeSpan m_Position = new AtomicTimeSpan(default);
        private readonly AtomicTimeSpan m_PositionNext = new AtomicTimeSpan(default);
        private readonly AtomicTimeSpan m_PositionCurrent = new AtomicTimeSpan(default);
        private readonly AtomicTimeSpan m_PositionPrevious = new AtomicTimeSpan(default);

        private readonly AtomicDouble m_SpeedRatio = new AtomicDouble(Constants.Controller.DefaultSpeedRatio);
        private readonly AtomicDouble m_Volume = new AtomicDouble(Constants.Controller.DefaultVolume);
        private readonly AtomicDouble m_Balance = new AtomicDouble(Constants.Controller.DefaultBalance);
        private readonly AtomicBoolean m_IsMuted = new AtomicBoolean(false);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaEngineState" /> class.
        /// </summary>
        /// <param name="mediaCore">The associated media core.</param>
        internal MediaEngineState(MediaEngine mediaCore)
        {
            MediaCore = mediaCore;
            ResetAll();
        }

        #endregion

        #region Controller Properties

        /// <summary>
        /// Gets or Sets the Source on this MediaElement.
        /// The Source property is the Uri of the media to be played.
        /// </summary>
        public Uri Source { get; private set; }

        /// <summary>
        /// Gets or sets the requested, non-guaranteed current SpeedRatio property of the media.
        /// </summary>
        public double SpeedRatio
        {
            get => m_SpeedRatio.Value;
            set => m_SpeedRatio.Value = value;
        }

        /// <summary>
        /// Gets or sets the requested, non-guaranteed current Volume property on the MediaElement from 0 to 1.
        /// </summary>
        public double Volume
        {
            get => m_Volume.Value;
            set => m_Volume.Value = value;
        }

        /// <summary>
        /// Gets or sets the requested, non-guaranteed current Balance property on the MediaElement.
        /// </summary>
        public double Balance
        {
            get => m_Balance.Value;
            set => m_Balance.Value = value;
        }

        /// <summary>
        /// Gets or sets the requested, non-guaranteed current IsMuted property on the MediaElement.
        /// </summary>
        public bool IsMuted
        {
            get => m_IsMuted.Value;
            set => m_IsMuted.Value = value;
        }

        #endregion

        #region Renderer Update Driven Properties

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        public PlaybackStatus MediaState
        {
            get => (PlaybackStatus)m_MediaState.Value;
            private set => m_MediaState.Value = (int)value;
        }

        /// <summary>
        /// Gets or Sets the Position property on the MediaElement.
        /// </summary>
        public TimeSpan Position
        {
            get => m_Position.Value;
            private set => m_Position.Value = value;
        }

        /// <summary>
        /// Gets a value indicating whether the media has reached its end.
        /// </summary>
        public bool HasMediaEnded
        {
            get => m_HasMediaEnded.Value;
            private set => m_HasMediaEnded.Value = value;
        }

        /// <summary>
        /// Returns the current video SMTPE timecode if available.
        /// </summary>
        public string VideoSmtpeTimecode { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the name of the video hardware decoder in use.
        /// Enabling hardware acceleration does not guarantee decoding will be performed in hardware.
        /// When hardware decoding of frames is in use this will return the name of the HW accelerator.
        /// Otherwise it will return an empty string.
        /// </summary>
        public string VideoHardwareDecoder { get; private set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the current video stream has closed captions
        /// </summary>
        public bool HasClosedCaptions { get; private set; }

        /// <summary>
        /// Gets the discrete timestamp of the next frame.
        /// </summary>
        public TimeSpan PositionNext
        {
            get => m_PositionNext.Value;
            private set => m_PositionNext.Value = value;
        }

        /// <summary>
        /// Gets the discrete timestamp of the current frame.
        /// </summary>
        public TimeSpan PositionCurrent
        {
            get => m_PositionCurrent.Value;
            private set => m_PositionCurrent.Value = value;
        }

        /// <summary>
        /// Gets the discrete timestamp of the previous frame.
        /// </summary>
        public TimeSpan PositionPrevious
        {
            get => m_PositionPrevious.Value;
            private set => m_PositionPrevious.Value = value;
        }

        #endregion

        #region Self-Updating Properties

        /// <summary>
        /// Gets a value indicating whether the media clock is playing.
        /// </summary>
        public bool IsPlaying => IsOpen && MediaCore.Clock.IsRunning;

        /// <summary>
        /// Gets a value indicating whether the media clock is paused.
        /// </summary>
        public bool IsPaused => IsOpen && (MediaCore.Clock.IsRunning == false);

        /// <summary>
        /// Gets a value indicating whether the media seeking is in progress.
        /// </summary>
        public bool IsSeeking => MediaCore.Commands?.IsSeeking ?? false;

        /// <summary>
        /// Gets a value indicating whether the media is in the process of closing media.
        /// </summary>
        public bool IsClosing => MediaCore.Commands?.IsClosing ?? false;

        /// <summary>
        /// Gets a value indicating whether the media is in the process of opening.
        /// </summary>
        public bool IsOpening => MediaCore.Commands?.IsOpening ?? false;

        /// <summary>
        /// Gets a value indicating whether the media is currently changing its components.
        /// </summary>
        public bool IsChanging => MediaCore.Commands?.IsChanging ?? false;

        #endregion

        #region Container Fixed, One-Time Properties

        /// <summary>
        /// Gets a value indicating whether this media element
        /// currently has an open media url.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Gets the duration of a single frame step on the main component.
        /// </summary>
        public TimeSpan PositionStep { get; private set; }

        /// <summary>
        /// Gets the stream's bitrate. Returns 0 if unavaliable.
        /// </summary>
        public long Bitrate { get; private set; }

        /// <summary>
        /// Provides key-value pairs of the metadata contained in the media.
        /// Returns null when media has not been loaded.
        /// </summary>
        public ReadOnlyDictionary<string, string> Metadata { get; private set; }

        /// <summary>
        /// Returns whether the currently loaded media can be paused.
        /// This is only valid after the MediaOpened event has fired.
        /// Note that this property is computed based on wether the stream is detected to be a live stream.
        /// </summary>
        public bool CanPause { get; private set; }

        /// <summary>
        /// Gets the media format. Returns null when media has not been loaded.
        /// </summary>
        public string MediaFormat { get; private set; }

        /// <summary>
        /// Gets the size in bytes of the current stream being read.
        /// For multi-file streams, get the size of the current file only.
        /// </summary>
        public long MediaStreamSize { get; private set; }

        /// <summary>
        /// Gets the index of the video stream.
        /// </summary>
        public int VideoStreamIndex { get; private set; }

        /// <summary>
        /// Gets the index of the audio stream.
        /// </summary>
        public int AudioStreamIndex { get; private set; }

        /// <summary>
        /// Gets the index of the subtitle stream.
        /// </summary>
        public int SubtitleStreamIndex { get; private set; }

        /// <summary>
        /// Returns whether the given media has audio.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public bool HasAudio { get; private set; }

        /// <summary>
        /// Returns whether the given media has video. Only valid after the
        /// MediaOpened event has fired.
        /// </summary>
        public bool HasVideo { get; private set; }

        /// <summary>
        /// Returns whether the given media has subtitles (in stream or preloaded). Only valid after the
        /// MediaOpened event has fired.
        /// </summary>
        public bool HasSubtitles { get; private set; }

        /// <summary>
        /// Gets the video codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string VideoCodec { get; private set; }

        /// <summary>
        /// Gets the video bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public long VideoBitrate { get; private set; }

        /// <summary>
        /// Gets the video display rotation.
        /// </summary>
        public double VideoRotation { get; private set; }

        /// <summary>
        /// Returns the natural width of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoWidth { get; private set; }

        /// <summary>
        /// Returns the natural height of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoHeight { get; private set; }

        /// <summary>
        /// Returns the current video aspect ratio if available.
        /// </summary>
        public string VideoAspectRatio { get; private set; }

        /// <summary>
        /// Gets the video frame rate in which all timestamps can be represented.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameRate { get; private set; }

        /// <summary>
        /// Gets the audio codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string AudioCodec { get; private set; }

        /// <summary>
        /// Gets the audio bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public long AudioBitrate { get; private set; }

        /// <summary>
        /// Gets the audio channels count.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioChannels { get; private set; }

        /// <summary>
        /// Gets the audio sample rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioSampleRate { get; private set; }

        /// <summary>
        /// Gets the audio bits per sample.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitsPerSample { get; private set; }

        /// <summary>
        /// Gets the Media's natural duration
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public TimeSpan? NaturalDuration { get; private set; }

        /// <summary>
        /// Returns whether the currently loaded media is live or real-time and does not have a set duration
        /// This is only valid after the MediaOpened event has fired.
        /// </summary>
        public bool IsLiveStream { get; private set; }

        /// <summary>
        /// Returns whether the currently loaded media is a network stream.
        /// This is only valid after the MediaOpened event has fired.
        /// </summary>
        public bool IsNetworkStream { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the currently loaded media can be seeked.
        /// </summary>
        public bool IsSeekable { get; private set; }

        #endregion

        #region State Method Managed Media Properties

        /// <summary>
        /// Get a value indicating whether the media is buffering.
        /// </summary>
        public bool IsBuffering
        {
            get => m_IsBuffering.Value;
            private set => m_IsBuffering.Value = value;
        }

        /// <summary>
        /// Gets the instantaneous, compressed bitrate of the decoders for the currently active component streams.
        /// This is provided in bits per second.
        /// </summary>
        public long DecodingBitrate
        {
            get => m_DecodingBitrate.Value;
            private set => m_DecodingBitrate.Value = value;
        }

        /// <summary>
        /// Gets a value that indicates the percentage of buffering progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double BufferingProgress
        {
            get => m_BufferingProgress.Value;
            private set => m_BufferingProgress.Value = value;
        }

        /// <summary>
        /// Gets a value that indicates the percentage of download progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double DownloadProgress
        {
            get => m_DownloadProgress.Value;
            private set => m_DownloadProgress.Value = value;
        }

        /// <summary>
        /// Gets the current byte length of the buffered packets
        /// </summary>
        public long PacketBufferLength
        {
            get => m_PacketBufferLength.Value;
            private set => m_PacketBufferLength.Value = value;
        }

        /// <summary>
        /// The number of packets in the buffer for all media components.
        /// </summary>
        public int PacketBufferCount
        {
            get => m_PacketBufferCount.Value;
            private set => m_PacketBufferCount.Value = value;
        }

        #endregion

        #region State Management Methods

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
            Bitrate = MediaCore.Container?.MediaBitrate ?? default;
            IsOpen = (IsOpening == false) && (MediaCore.Container?.IsOpen ?? default);
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
            VideoBitrate = MediaCore.Container?.Components.Video?.Bitrate ?? default;
            VideoRotation = MediaCore.Container?.Components.Video?.DisplayRotation ?? default;
            NaturalVideoWidth = MediaCore.Container?.Components.Video?.FrameWidth ?? default;
            NaturalVideoHeight = MediaCore.Container?.Components.Video?.FrameHeight ?? default;
            VideoFrameRate = MediaCore.Container?.Components.Video?.AverageFrameRate ?? default;
            AudioCodec = MediaCore.Container?.Components.Audio?.CodecName;
            AudioBitrate = MediaCore.Container?.Components.Audio?.Bitrate ?? default;
            AudioChannels = MediaCore.Container?.Components.Audio?.Channels ?? default;
            AudioSampleRate = MediaCore.Container?.Components.Audio?.SampleRate ?? default;
            AudioBitsPerSample = MediaCore.Container?.Components.Audio?.BitsPerSample ?? default;
            NaturalDuration = MediaCore.Container?.MediaDuration;
            IsLiveStream = MediaCore.Container?.IsLiveStream ?? default;
            IsNetworkStream = MediaCore.Container?.IsNetworkStream ?? default;
            IsSeekable = MediaCore.Container?.IsStreamSeekable ?? default;
            CanPause = IsOpen ? (IsLiveStream == false) : default;

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
                    PositionStep = TimeSpan.FromTicks(Convert.ToInt64(
                        TimeSpan.TicksPerMillisecond * 1000d / (main as VideoComponent).BaseFrameRate));
                    break;

                case MediaType.None:
                case MediaType.Subtitle:
                default:
                    PositionStep = default;
                    break;
            }
        }

        /// <summary>
        /// Updates state properties coming from a new media block.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="main">The main MediaType</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateDynamicBlockProperties(MediaBlock block, MediaBlockBuffer buffer, MediaType main)
        {
            if (block == null) return;

            // Update PositionCurrent, PositionNext and PositionPrevious
            if (block.MediaType == main)
            {
                PositionCurrent = block.StartTime;
                buffer.Neighbors(block, out MediaBlock previous, out MediaBlock next);
                PositionNext = next?.StartTime ?? TimeSpan.FromTicks(
                    block.EndTime.Ticks + (block.Duration.Ticks / 2));
                PositionPrevious = previous?.StartTime ?? TimeSpan.FromTicks(
                    block.StartTime.Ticks - (block.Duration.Ticks / 2));
            }

            // Update video block properties
            if (block is VideoBlock videoBlock)
            {
                // I don't know of any codecs changing the width and the height dynamically
                // but we update the properties just to be safe.
                NaturalVideoWidth = videoBlock.PixelWidth;
                NaturalVideoHeight = videoBlock.PixelHeight;

                // Update the has closed captions state as it might come in later
                // as frames are decoded
                if (HasClosedCaptions == false && videoBlock.ClosedCaptions.Count > 0)
                    HasClosedCaptions = true;

                VideoSmtpeTimecode = videoBlock.SmtpeTimecode;
                VideoHardwareDecoder = videoBlock.IsHardwareFrame ?
                    videoBlock.HardwareAcceleratorName : string.Empty;
            }
        }

        /// <summary>
        /// Updates the media ended state and notifies the parent if there is a change from false to true.
        /// </summary>
        /// <param name="hasEnded">if set to <c>true</c> [has ended].</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateMediaEnded(bool hasEnded)
        {
            if (HasMediaEnded == false && hasEnded == true)
            {
                HasMediaEnded = true;
                MediaCore.SendOnMediaEnded();
                return;
            }

            HasMediaEnded = hasEnded;
        }

        /// <summary>
        /// Updates the position related properies.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdatePosition() => UpdatePosition(MediaCore.WallClock);

        /// <summary>
        /// Updates the position related properties.
        /// </summary>
        /// <param name="newPosition">The new position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdatePosition(TimeSpan newPosition)
        {
            var oldSpeedRatio = MediaCore.Clock.SpeedRatio;
            var newSpeedRatio = SpeedRatio;

            if (oldSpeedRatio != newSpeedRatio)
                MediaCore.Clock.SpeedRatio = SpeedRatio;

            var oldPosition = Position;
            if (oldPosition.Ticks == newPosition.Ticks)
                return;

            Position = newPosition;

            var blockCount = MediaCore.Blocks.Main(MediaCore.Container)?.Count ?? 0;
            if (blockCount <= 0)
            {
                PositionCurrent = default;
                PositionNext = default;
                PositionPrevious = default;
            }

            MediaCore.SendOnPositionChanged(oldPosition, newPosition);
        }

        /// <summary>
        /// Resets all media state properties
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResetAll()
        {
            ResetMediaProperties();
            UpdateFixedContainerProperties();
            InitializeBufferingStatistics();
        }

        /// <summary>
        /// Updates the MediaState property.
        /// </summary>
        /// <param name="mediaState">State of the media.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateMediaState(PlaybackStatus mediaState)
        {
            var oldValue = MediaState;
            if (oldValue == mediaState)
                return;

            MediaState = mediaState;
            MediaCore.SendOnMediaStateChanged(oldValue, mediaState);
        }

        /// <summary>
        /// Resets the controller properies.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResetMediaProperties()
        {
            var oldMediaState = MediaState;
            var newMediaState = PlaybackStatus.Close;

            // Reset Method-controlled properties
            MediaState = newMediaState;
            Position = default;
            PositionCurrent = default;
            PositionNext = default;
            PositionPrevious = default;
            HasMediaEnded = default;

            // Reset decoder and buffering
            ResetBufferingStatistics();

            VideoSmtpeTimecode = string.Empty;
            VideoHardwareDecoder = string.Empty;

            // Reset controller poperties
            SpeedRatio = Constants.Controller.DefaultSpeedRatio;

            if (oldMediaState != newMediaState)
                MediaCore.SendOnMediaStateChanged(oldMediaState, newMediaState);
        }

        /// <summary>
        /// Resets all the buffering properties to their defaults.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InitializeBufferingStatistics()
        {
            // var fileSize = ffmpeg.avio_size(Parent.Container.InputContext->pb);
            const long MinimumValidFileSize = 1024 * 1024; // 1 Mbytes

            // Start with default values
            ResetBufferingStatistics();

            // Reset the properties if the is no associated container
            if (MediaCore.Container == null)
            {
                MediaStreamSize = default;
                return;
            }

            // Try to get a valid stream size
            var durationSeconds = NaturalDuration.HasValue ? NaturalDuration.Value.TotalSeconds : 0d;
            MediaStreamSize = MediaCore.Container?.MediaStreamSize ?? default;

            // Compute the bitrate and buffering properties based on media byte size
            if (MediaStreamSize >= MinimumValidFileSize && IsSeekable && durationSeconds > 0)
            {
                // The bitrate is simply the media size over the total duration
                Bitrate = Convert.ToInt64(8d * MediaStreamSize / NaturalDuration.Value.TotalSeconds);
            }
        }

        /// <summary>
        /// Updates the decoding bitrate.
        /// </summary>
        /// <param name="bitrate">The bitrate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateDecodingBitrate(long bitrate) => DecodingBitrate = bitrate;

        /// <summary>
        /// Updates the buffering properties: <see cref="PacketBufferCount" />, <see cref="PacketBufferLength" />,
        /// <see cref="IsBuffering" />, <see cref="BufferingProgress" />, <see cref="DownloadProgress" />.
        /// If a change is detected on the <see cref="IsBuffering" /> property then a notification is sent.
        /// </summary>
        /// <param name="bufferLength">Length of the packet buffer.</param>
        /// <param name="bufferCount">The packet buffer count.</param>
        /// <param name="bufferCountMax">The packet buffer count maximum for all components</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateBufferingStats(long bufferLength, int bufferCount, int bufferCountMax)
        {
            PacketBufferCount = bufferCount;
            PacketBufferLength = bufferLength;
            BufferingProgress = bufferCountMax <= 0 ? 0 : Math.Min(1d, (double)bufferCount / bufferCountMax);
            DownloadProgress = Math.Min(1d, (double)bufferLength / MediaCore.BufferLengthMax);

            // Check if we are currently buffering
            var isCurrentlyBuffering = MediaCore.ShouldReadMorePackets
                && (MediaCore.IsSyncBuffering || BufferingProgress < 1d);

            // Detect and notify a change in buffering state
            if (isCurrentlyBuffering != IsBuffering)
            {
                IsBuffering = isCurrentlyBuffering;
                if (isCurrentlyBuffering)
                    MediaCore.SendOnBufferingStarted();
                else
                    MediaCore.SendOnBufferingEnded();
            }
        }

        /// <summary>
        /// Resets the buffering statistics.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetBufferingStatistics()
        {
            IsBuffering = default;
            DecodingBitrate = default;
            BufferingProgress = default;
            DownloadProgress = default;
            PacketBufferLength = default;
            PacketBufferCount = default;
        }

        #endregion
    }
}
