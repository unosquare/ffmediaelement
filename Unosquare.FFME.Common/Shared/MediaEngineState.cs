namespace Unosquare.FFME.Shared
{
    using Primitives;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Contains all the status properties of the stream being handled by the media engine.
    /// </summary>
    public sealed class MediaEngineState : IMediaEngineState
    {
        #region Property Backing and Private State

        private const ulong NetworkStreamCacheFactor = 30;
        private const ulong StandardStreamCacheFactor = 4;

        private static readonly TimeSpan GenericFrameStepDuration = TimeSpan.FromSeconds(0.01d);
        private static readonly PropertyInfo[] Properties = null;

        private readonly MediaEngine Parent = null;
        private readonly ReadOnlyDictionary<string, string> EmptyDictionary
            = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        private readonly AtomicInteger m_MediaState = new AtomicInteger((int)PlaybackStatus.Close);

        private readonly AtomicBoolean m_IsOpen = new AtomicBoolean(default);
        private readonly AtomicBoolean m_HasMediaEnded = new AtomicBoolean(default);
        private readonly AtomicBoolean m_IsBuffering = new AtomicBoolean(default);

        private readonly AtomicDouble m_BufferingProgress = new AtomicDouble(default);
        private readonly AtomicDouble m_DownloadProgress = new AtomicDouble(default);

        private readonly AtomicULong m_BufferCacheLength = new AtomicULong(default);
        private readonly AtomicULong m_DownloadCacheLength = new AtomicULong(default);

        private readonly AtomicTimeSpan m_Position = new AtomicTimeSpan(default);
        private readonly AtomicTimeSpan m_PositionNext = new AtomicTimeSpan(default);
        private readonly AtomicTimeSpan m_PositionCurrent = new AtomicTimeSpan(default);
        private readonly AtomicTimeSpan m_PositionPrevious = new AtomicTimeSpan(default);

        /// <summary>
        /// Gets the guessed buffered bytes in the packet queue per second.
        /// If bitrate information is available, then it returns the bitrate converted to byte rate.
        /// Returns null if it has not been guessed.
        /// </summary>
        private ulong? GuessedByteRate;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes static members of the <see cref="MediaEngineState" /> class.
        /// </summary>
        static MediaEngineState() =>
            Properties = typeof(MediaEngineState).GetProperties(BindingFlags.Instance | BindingFlags.Public);

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaEngineState" /> class.
        /// </summary>
        /// <param name="parent">The parent.</param>
        internal MediaEngineState(MediaEngine parent)
        {
            Parent = parent;
            ResetMediaProperties();
            UpdateFixedContainerProperties();
            InitializeBufferingProperties();
        }

        #endregion

        #region Controller Properties

        /// <summary>
        /// Gets or Sets the SpeedRatio property of the media.
        /// </summary>
        public double SpeedRatio { get; set; }

        /// <summary>
        /// Gets or Sets the Source on this MediaElement.
        /// The Source property is the Uri of the media to be played.
        /// </summary>
        public Uri Source { get; internal set; }

        /// <summary>
        /// Gets/Sets the Volume property on the MediaElement.
        /// Note: Valid values are from 0 to 1
        /// </summary>
        public double Volume { get; set; } = Constants.Controller.DefaultVolume;

        /// <summary>
        /// Gets/Sets the Balance property on the MediaElement.
        /// </summary>
        public double Balance { get; set; } = Constants.Controller.DefaultBalance;

        /// <summary>
        /// Gets/Sets the IsMuted property on the MediaElement.
        /// </summary>
        public bool IsMuted { get; set; } = false;

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
        /// If not available, this property returns an empty string.
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
        /// Gets the duration of a single frame step.
        /// If there is a video component with a framerate, this propery returns the length of a frame.
        /// If there is no video component it simply returns 10 milliseconds.
        /// </summary>
        public TimeSpan FrameStepDuration
        {
            get
            {
                var frameLengthMillis = 1000d * VideoFrameLength;

                if (frameLengthMillis <= 0)
                    return IsOpen ? GenericFrameStepDuration : TimeSpan.Zero;

                return TimeSpan.FromTicks(Convert.ToInt64(TimeSpan.TicksPerMillisecond * frameLengthMillis));
            }
        }

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
        public bool IsPlaying => IsOpen && (Parent?.Clock.IsRunning ?? default);

        /// <summary>
        /// Gets a value indicating whether the media clock is paused.
        /// </summary>
        public bool IsPaused => IsOpen && (Parent?.Clock.IsRunning ?? true) == false;

        /// <summary>
        /// Gets a value indicating whether the media seeking is in progress.
        /// </summary>
        public bool IsSeeking => Parent?.Commands?.IsSeeking ?? false;

        /// <summary>
        /// Gets a value indicating whether the media is in the process of closing media.
        /// </summary>
        public bool IsClosing => Parent?.Commands?.IsClosing ?? false;

        /// <summary>
        /// Gets a value indicating whether the media is in the process of opening.
        /// </summary>
        public bool IsOpening => Parent?.Commands?.IsOpening ?? false;

        /// <summary>
        /// Gets a value indicating whether the media is currently changing its components.
        /// </summary>
        public bool IsChanging => Parent?.Commands?.IsChanging ?? false;

        #endregion

        #region Container Fixed, One-Time Properties

        /// <summary>
        /// Gets a value indicating whether this media element
        /// currently has an open media url.
        /// </summary>
        public bool IsOpen
        {
            get => m_IsOpen.Value;
            private set => m_IsOpen.Value = value;
        }

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
        public ulong VideoBitrate { get; private set; }

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
        /// Gets the video frame rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameRate { get; private set; }

        /// <summary>
        /// Gets the duration in seconds of the video frame.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameLength { get; private set; }

        /// <summary>
        /// Gets the audio codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string AudioCodec { get; private set; }

        /// <summary>
        /// Gets the audio bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public ulong AudioBitrate { get; private set; }

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
        public bool IsNetowrkStream { get; private set; }

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
        /// Gets a value that indicates the percentage of buffering progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double BufferingProgress
        {
            get => m_BufferingProgress.Value;
            private set => m_BufferingProgress.Value = value;
        }

        /// <summary>
        /// The packet buffer length.
        /// It is adjusted to 1 second if bitrate information is available.
        /// Otherwise, it's simply 512KB and it is guessed later on.
        /// </summary>
        public ulong BufferCacheLength
        {
            get => m_BufferCacheLength.Value;
            private set => m_BufferCacheLength.Value = value;
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
        /// Gets the maximum packet buffer length, according to the bitrate (if available).
        /// If it's a realtime stream it will return 30 times the buffer cache length.
        /// Otherwise, it will return  4 times of the buffer cache length.
        /// </summary>
        public ulong DownloadCacheLength
        {
            get => m_DownloadCacheLength.Value;
            private set => m_DownloadCacheLength.Value = value;
        }

        #endregion

        #region State Management Methods

        /// <summary>
        /// Updates the fixed container properties.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateFixedContainerProperties()
        {
            IsOpen = (IsOpening == false) && (Parent.Container?.IsOpen ?? default);
            Metadata = Parent.Container?.Metadata ?? EmptyDictionary;
            MediaFormat = Parent.Container?.MediaFormatName;
            VideoStreamIndex = Parent.Container?.Components.Video?.StreamIndex ?? -1;
            AudioStreamIndex = Parent.Container?.Components.Audio?.StreamIndex ?? -1;
            SubtitleStreamIndex = Parent.Container?.Components.Subtitles?.StreamIndex ?? -1;
            HasAudio = Parent.Container?.Components.HasAudio ?? default;
            HasVideo = Parent.Container?.Components.HasVideo ?? default;
            HasClosedCaptions = Parent.Container?.Components.Video?.StreamInfo?.HasClosedCaptions ?? default;
            HasSubtitles = (Parent.PreloadedSubtitles?.Count ?? 0) > 0
                || (Parent.Container?.Components.HasSubtitles ?? false);
            VideoCodec = Parent.Container?.Components.Video?.CodecName;
            VideoBitrate = Parent.Container?.Components.Video?.Bitrate ?? default;
            VideoRotation = Parent.Container?.Components.Video?.DisplayRotation ?? default;
            NaturalVideoWidth = Parent.Container?.Components.Video?.FrameWidth ?? default;
            NaturalVideoHeight = Parent.Container?.Components.Video?.FrameHeight ?? default;
            VideoFrameRate = Parent.Container?.Components.Video?.BaseFrameRate ?? default;
            VideoFrameLength = VideoFrameRate <= 0 ? default : 1d / VideoFrameRate;
            AudioCodec = Parent.Container?.Components.Audio?.CodecName;
            AudioBitrate = Parent.Container?.Components.Audio?.Bitrate ?? default;
            AudioChannels = Parent.Container?.Components.Audio?.Channels ?? default;
            AudioSampleRate = Parent.Container?.Components.Audio?.SampleRate ?? default;
            AudioBitsPerSample = Parent.Container?.Components.Audio?.BitsPerSample ?? default;
            NaturalDuration = Parent.Container?.MediaDuration;
            IsLiveStream = Parent.Container?.IsLiveStream ?? default;
            IsNetowrkStream = Parent.Container?.IsNetworkStream ?? default;
            IsSeekable = Parent.Container?.IsStreamSeekable ?? default;
            CanPause = IsOpen ? (IsLiveStream == false) : default;
        }

        /// <summary>
        /// Updates state properties coming from a new media block.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="main">The main MediaType</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateDynamicBlockProperties(MediaBlock block, MediaType main)
        {
            if (block == null) return;

            // TODO: Still missing current, previous and next positions here
            if (block is VideoBlock videoBlock)
            {
                // TODO: I don't know of any codecs changing the widht and the height dynamically
                // NaturalVideoWidth = videoBlock.PixelWidth;
                // NaturalVideoHeight = videoBlock.PixelHeight;
                if (HasClosedCaptions == false && videoBlock.ClosedCaptions.Count > 0)
                    HasClosedCaptions = true;

                VideoSmtpeTimecode = videoBlock.SmtpeTimecode;
                VideoHardwareDecoder = (Parent.Container?.Components.Video?.IsUsingHardwareDecoding ?? false) ?
                    Parent.Container?.Components.Video?.HardwareAccelerator?.Name ?? string.Empty : string.Empty;
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
                SignalBufferingEnded();
                HasMediaEnded = true;
                Parent?.SendOnMediaEnded();
                return;
            }

            HasMediaEnded = hasEnded;
        }

        /// <summary>
        /// Updates the position.
        /// </summary>
        /// <param name="newPosition">The new position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdatePosition(TimeSpan newPosition)
        {
            var oldPosition = Position;
            if (oldPosition.Ticks == newPosition.Ticks)
                return;

            Position = newPosition;
            Parent.SendOnPositionChanged(oldPosition, newPosition);

            var main = Parent.Container?.Components?.Main.MediaType ?? MediaType.None;
            var blocks = Parent.Blocks[main];
            var currentMainBlock = blocks[newPosition];

            if (currentMainBlock == null || blocks == null)
            {
                PositionCurrent = default;
                PositionNext = default;
                PositionPrevious = default;
            }
            else
            {
                blocks.Neighbors(currentMainBlock, out var previous, out var next);
                PositionCurrent = currentMainBlock.StartTime;
                PositionNext = next?.StartTime ?? TimeSpan.FromTicks(
                    currentMainBlock.EndTime.Ticks + (currentMainBlock.Duration.Ticks / 2));
                PositionPrevious = previous?.StartTime ?? TimeSpan.FromTicks(
                    currentMainBlock.StartTime.Ticks - (currentMainBlock.Duration.Ticks / 2));
            }
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
            Parent.SendOnMediaStateChanged(oldValue, mediaState);
        }

        /// <summary>
        /// Resets the controller properies.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResetMediaProperties()
        {
            var oldMediaState = default(PlaybackStatus);
            var newMediaState = PlaybackStatus.Close;

            // Reset Method-controlled properties
            oldMediaState = MediaState;

            MediaState = newMediaState;
            Position = default;
            PositionCurrent = default;
            PositionNext = default;
            PositionPrevious = default;
            HasMediaEnded = default;
            IsBuffering = default;
            BufferingProgress = default;
            BufferCacheLength = default;
            DownloadProgress = default;
            DownloadCacheLength = default;

            VideoSmtpeTimecode = string.Empty;
            VideoHardwareDecoder = string.Empty;

            // Reset volatile controller poperties
            SpeedRatio = Constants.Controller.DefaultSpeedRatio;

            if (oldMediaState != newMediaState)
                Parent.SendOnMediaStateChanged(oldMediaState, newMediaState);
        }

        /// <summary>
        /// Resets all the buffering properties to their defaults.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InitializeBufferingProperties()
        {
            const int MinimumValidBitrate = 96 * 1000; // 96kbps
            const int StartingCacheLength = 512 * 1024; // Half a megabyte

            GuessedByteRate = default;

            if (Parent.Container == null)
            {
                IsBuffering = default;
                BufferCacheLength = default;
                DownloadCacheLength = default;
                BufferingProgress = default;
                DownloadProgress = default;
                return;
            }

            var allComponentsHaveBitrate = true;

            if (HasAudio && AudioBitrate <= 0)
                allComponentsHaveBitrate = false;

            if (HasVideo && VideoBitrate <= 0)
                allComponentsHaveBitrate = false;

            if (HasAudio == false && HasVideo == false)
                allComponentsHaveBitrate = false;

            // The metadata states that we have bitrates for the components
            // but sometimes (like in certain WMV files) we have slightly incorrect information
            // and therefore, we multiply times 2 just to be safe
            var mediaBitrate = 2d * Math.Max(Parent.Container.MediaBitrate,
                allComponentsHaveBitrate ? AudioBitrate + VideoBitrate : 0);

            if (mediaBitrate > MinimumValidBitrate)
            {
                BufferCacheLength = Convert.ToUInt64(mediaBitrate / 8d);
                GuessedByteRate = Convert.ToUInt64(BufferCacheLength);
            }
            else
            {
                BufferCacheLength = StartingCacheLength;
            }

            DownloadCacheLength = BufferCacheLength * (IsNetowrkStream ?
                NetworkStreamCacheFactor : StandardStreamCacheFactor);
            IsBuffering = false;
            BufferingProgress = 0;
            DownloadProgress = 0;
        }

        /// <summary>
        /// Signals the buffering started.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SignalBufferingStarted()
        {
            if (IsBuffering) return;
            else IsBuffering = true;

            Parent?.SendOnBufferingStarted();
        }

        /// <summary>
        /// Signals the buffering ended.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SignalBufferingEnded()
        {
            if (IsBuffering == false) return;
            else IsBuffering = false;

            Parent?.SendOnBufferingEnded();
        }

        /// <summary>
        /// Updates the buffering properties: IsBuffering, BufferingProgress, DownloadProgress.
        /// </summary>
        /// <param name="packetBufferLength">Length of the packet buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateBufferingProgress(double packetBufferLength)
        {
            bool wasBuffering = default;

            // Capture the current state
            wasBuffering = IsBuffering;

            // Update the buffering progress
            BufferingProgress = BufferCacheLength != 0 ? Math.Min(
                1d, Math.Round(packetBufferLength / BufferCacheLength, 3)) : 0;

            // Update the download progress
            DownloadProgress = DownloadCacheLength != 0 ? Math.Min(
                1d, Math.Round(packetBufferLength / DownloadCacheLength, 3)) : 0;

            // Compute the new state
            IsBuffering = packetBufferLength < BufferCacheLength
                && (Parent?.CanReadMorePackets ?? false);

            // Notify the change
            if (wasBuffering && IsBuffering == false)
                Parent?.SendOnBufferingEnded();
        }

        /// <summary>
        /// Guesses the bitrate of the input stream.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GuessBufferingProperties()
        {
            if (GuessedByteRate != null || Parent.Container == null || Parent.Container.Components == null)
                return;

            // Capture the read bytes of a 1-second buffer
            var bytesReadSoFar = Parent.Container.Components.LifetimeBytesRead;
            var shortestDuration = TimeSpan.MaxValue;
            var currentDuration = TimeSpan.Zero;

            foreach (var t in Parent.Container.Components.MediaTypes)
            {
                if (t != MediaType.Audio && t != MediaType.Video)
                    continue;

                currentDuration = Parent.Blocks[t].LifetimeBlockDuration;

                if (currentDuration.TotalSeconds < 1)
                {
                    shortestDuration = TimeSpan.Zero;
                    break;
                }

                if (currentDuration < shortestDuration)
                    shortestDuration = currentDuration;
            }

            if (shortestDuration.TotalSeconds >= 1 && shortestDuration != TimeSpan.MaxValue)
            {
                // We make the byterate 20% larget than what we have received, just to be safe.
                GuessedByteRate = (ulong)(1.2 * bytesReadSoFar / shortestDuration.TotalSeconds);
                BufferCacheLength = Convert.ToUInt64(GuessedByteRate);
                DownloadCacheLength = BufferCacheLength * (IsNetowrkStream ? NetworkStreamCacheFactor : StandardStreamCacheFactor);
            }
        }

        #endregion
    }
}
