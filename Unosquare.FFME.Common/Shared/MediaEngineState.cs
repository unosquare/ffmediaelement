namespace Unosquare.FFME.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using Unosquare.FFME.Primitives;

    /// <summary>
    /// Contains all the status properties of the stream being handled by the media engine.
    /// </summary>
    public sealed class MediaEngineState
    {
        #region Property Backing and Private State

        private const int NetworkStreamCacheFactor = 30;
        private const int StandardStreamCacheFactor = 4;

        private static PropertyInfo[] Properties = null;
        private readonly MediaEngine Parent = null;
        private readonly ReadOnlyDictionary<string, string> EmptyDictionary
            = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        private AtomicBoolean m_IsSeeking = new AtomicBoolean(false);

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
        static MediaEngineState()
        {
            Properties = typeof(MediaEngineState).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaEngineState" /> class.
        /// </summary>
        /// <param name="parent">The parent.</param>
        internal MediaEngineState(MediaEngine parent)
        {
            Parent = parent;
        }

        #endregion

        #region Volatile Controller Properties

        /// <summary>
        /// Gets or Sets the SpeedRatio property of the media.
        /// </summary>
        public double SpeedRatio { get; set; }

        /// <summary>
        /// Gets or Sets the Position property on the MediaElement.
        /// </summary>
        public TimeSpan Position
        {
            get;
            private set;
        }

        #endregion

        #region Non-Volatile Contoller Properties

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

        #region Read-Only Media Properties

        /// <summary>
        /// Provides key-value pairs of the metadata contained in the media.
        /// Returns null when media has not been loaded.
        /// </summary>
        public ReadOnlyDictionary<string, string> Metadata => Parent.Container?.Metadata ?? EmptyDictionary;

        /// <summary>
        /// Gets the media format. Returns null when media has not been loaded.
        /// </summary>
        public string MediaFormat => Parent.Container?.MediaFormatName;

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

                if (HasVideo && VideoFrameLength > 0)
                    return TimeSpan.FromSeconds(VideoFrameLength);

                return TimeSpan.FromSeconds(0.1d);
            }
        }

        /// <summary>
        /// Returns whether the given media has audio.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public bool HasAudio => Parent.Container?.Components.HasAudio ?? false;

        /// <summary>
        /// Returns whether the given media has video. Only valid after the
        /// MediaOpened event has fired.
        /// </summary>
        public bool HasVideo => Parent.Container?.Components.HasVideo ?? false;

        /// <summary>
        /// Gets the video codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string VideoCodec => Parent.Container?.Components.Video?.CodecName;

        /// <summary>
        /// Gets the video bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int VideoBitrate => Parent.Container?.Components.Video?.Bitrate ?? 0;

        /// <summary>
        /// Returns the natural width of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoWidth => Parent.Container?.Components.Video?.FrameWidth ?? 0;

        /// <summary>
        /// Returns the natural height of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoHeight => Parent.Container?.Components.Video?.FrameHeight ?? 0;

        /// <summary>
        /// Gets the video frame rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameRate => Parent.Container?.Components.Video?.BaseFrameRate ?? 0;

        /// <summary>
        /// Gets the duration in seconds of the video frame.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameLength => 1d / VideoFrameRate;

        /// <summary>
        /// Gets the audio codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string AudioCodec => Parent.Container?.Components.Audio?.CodecName;

        /// <summary>
        /// Gets the audio bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitrate => Parent.Container?.Components.Audio?.Bitrate ?? 0;

        /// <summary>
        /// Gets the audio channels count.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioChannels => Parent.Container?.Components.Audio?.Channels ?? 0;

        /// <summary>
        /// Gets the audio sample rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioSampleRate => Parent.Container?.Components.Audio?.SampleRate ?? 0;

        /// <summary>
        /// Gets the audio bits per sample.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitsPerSample => Parent.Container?.Components.Audio?.BitsPerSample ?? 0;

        /// <summary>
        /// Gets the Media's natural duration
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public TimeSpan? NaturalDuration => Parent.Container?.MediaDuration;

        /// <summary>
        /// Returns whether the currently loaded media can be paused.
        /// This is only valid after the MediaOpened event has fired.
        /// Note that this property is computed based on wether the stream is detected to be a live stream.
        /// </summary>
        public bool CanPause => IsOpen ? (IsLiveStream == false) : false;

        /// <summary>
        /// Returns whether the currently loaded media is live or real-time and does not have a set duration
        /// This is only valid after the MediaOpened event has fired.
        /// </summary>
        public bool IsLiveStream => Parent.Container?.IsLiveStream ?? false;

        /// <summary>
        /// Returns whether the currently loaded media is a network stream.
        /// This is only valid after the MediaOpened event has fired.
        /// </summary>
        public bool IsNetowrkStream => Parent.Container?.IsNetworkStream ?? false;

        /// <summary>
        /// Gets a value indicating whether the currently loaded media can be seeked.
        /// </summary>
        public bool IsSeekable => Parent.Container?.IsStreamSeekable ?? false;

        /// <summary>
        /// Gets a value indicating whether the media is playing.
        /// </summary>
        public bool IsPlaying => MediaState == PlaybackStatus.Play;

        /// <summary>
        /// Gets a value indicating whether the media is paused.
        /// </summary>
        public bool IsPaused => MediaState == PlaybackStatus.Pause;

        /// <summary>
        /// Gets a value indicating whether this media element
        /// currently has an open media url.
        /// </summary>
        public bool IsOpen => (IsOpening == false) && (Parent.Container?.IsOpen ?? false);

        #endregion

        #region Settable Media Properties

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        public PlaybackStatus MediaState { get; private set; } = PlaybackStatus.Close;

        /// <summary>
        /// Gets a value indicating whether the media has reached its end.
        /// </summary>
        public bool HasMediaEnded { get; internal set; } = default(bool);

        /// <summary>
        /// Get a value indicating whether the media is buffering.
        /// </summary>
        public bool IsBuffering { get; internal set; } = default(bool);

        /// <summary>
        /// Gets a value indicating whether the media seeking is in progress.
        /// </summary>
        public bool IsSeeking
        {
            get => m_IsSeeking.Value == true;
            internal set => m_IsSeeking.Value = value;
        }

        /// <summary>
        /// Returns the current video SMTPE timecode if available.
        /// If not available, this property returns an empty string.
        /// </summary>
        public string VideoSmtpeTimecode { get; internal set; } = string.Empty;

        /// <summary>
        /// Gets the name of the video hardware decoder in use.
        /// Enabling hardware acceleration does not guarantee decoding will be performed in hardware.
        /// When hardware decoding of frames is in use this will return the name of the HW accelerator.
        /// Otherwise it will return an empty string.
        /// </summary>
        public string VideoHardwareDecoder { get; internal set; } = string.Empty;

        /// <summary>
        /// Gets a value that indicates the percentage of buffering progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double BufferingProgress { get; internal set; } = default(double);

        /// <summary>
        /// The packet buffer length.
        /// It is adjusted to 1 second if bitrate information is available.
        /// Otherwise, it's simply 512KB and it is guessed later on.
        /// </summary>
        public int BufferCacheLength { get; internal set; } = default(int);

        /// <summary>
        /// Gets a value that indicates the percentage of download progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double DownloadProgress { get; internal set; } = default(double);

        /// <summary>
        /// Gets the maximum packet buffer length, according to the bitrate (if available).
        /// If it's a realtime stream it will return 30 times the buffer cache length.
        /// Otherwise, it will return  4 times of the buffer cache length.
        /// </summary>
        public int DownloadCacheLength { get; internal set; } = default(int);

        /// <summary>
        /// Gets a value indicating whether the media is in the process of opening.
        /// </summary>
        public bool IsOpening { get; internal set; } = default(bool);

        #endregion

        /// <summary>
        /// Updates the position.
        /// </summary>
        /// <param name="position">The position.</param>
        internal void UpdatePosition(TimeSpan position)
        {
            var oldValue = Position;
            var newValue = position.Normalize();
            if (oldValue.Ticks == newValue.Ticks)
                return;

            Position = newValue;
            Parent.SendOnPositionChanged(oldValue, newValue);
        }

        /// <summary>
        /// Updates the MediaState property.
        /// </summary>
        /// <param name="mediaState">State of the media.</param>
        /// <param name="position">The new position value for this state.</param>
        internal void UpdateMediaState(PlaybackStatus mediaState, TimeSpan? position = null)
        {
            if (position != null)
                UpdatePosition(position.Value);

            var oldValue = MediaState;
            var newValue = mediaState;
            if (oldValue != newValue)
            {
                MediaState = newValue;
                Parent.SendOnMediaStateChanged(oldValue, newValue);
            }
        }

        /// <summary>
        /// Resets the controller properies.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResetMediaProperties()
        {
            // Reset Media Settable Properties
            UpdateMediaState(PlaybackStatus.Close, TimeSpan.Zero);
            HasMediaEnded = default(bool);
            IsBuffering = default(bool);
            IsSeeking = default(bool);
            VideoSmtpeTimecode = string.Empty;
            VideoHardwareDecoder = string.Empty;
            BufferingProgress = default(double);
            BufferCacheLength = default(int);
            DownloadProgress = default(double);
            DownloadCacheLength = default(int);
            IsOpening = default(bool);

            // Reset volatile controller poperties
            SpeedRatio = Constants.Controller.DefaultSpeedRatio;
        }

        /// <summary>
        /// Resets all the buffering properties to their defaults.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InitializeBufferingProperties()
        {
            const int MinimumValidBitrate = 512 * 1024; // 524kbps
            const int StartingCacheLength = 512 * 1024; // Half a megabyte

            GuessedByteRate = default(ulong?);

            if (Parent.Container == null)
            {
                IsBuffering = false;
                BufferCacheLength = 0;
                DownloadCacheLength = 0;
                BufferingProgress = 0;
                DownloadProgress = 0;
                return;
            }

            if (Parent.Container.MediaBitrate > MinimumValidBitrate)
            {
                BufferCacheLength = (int)Parent.Container.MediaBitrate / 8;
                GuessedByteRate = (ulong)BufferCacheLength;
            }
            else
            {
                BufferCacheLength = StartingCacheLength;
            }

            DownloadCacheLength = BufferCacheLength * (IsNetowrkStream ? NetworkStreamCacheFactor : StandardStreamCacheFactor);
            IsBuffering = false;
            BufferingProgress = 0;
            DownloadProgress = 0;
        }

        /// <summary>
        /// Updates the buffering properties: IsBuffering, BufferingProgress, DownloadProgress.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateBufferingProperties()
        {
            var packetBufferLength = Parent.Container?.Components.PacketBufferLength ?? 0d;

            // Update the buffering progress
            var bufferingProgress = Math.Min(
                1d, Math.Round(packetBufferLength / BufferCacheLength, 3));
            BufferingProgress = double.IsNaN(bufferingProgress) ? 0 : bufferingProgress;

            // Update the download progress
            var downloadProgress = Math.Min(
                1d, Math.Round(packetBufferLength / DownloadCacheLength, 3));
            DownloadProgress = double.IsNaN(downloadProgress) ? 0 : downloadProgress;

            // IsBuffering and BufferingProgress
            if (HasMediaEnded == false && Parent.CanReadMorePackets && (IsOpening || IsOpen))
            {
                var wasBuffering = IsBuffering;
                var isNowBuffering = packetBufferLength < BufferCacheLength;
                IsBuffering = isNowBuffering;

                if (wasBuffering == false && isNowBuffering)
                    Parent.SendOnBufferingStarted();
                else if (wasBuffering && isNowBuffering == false)
                    Parent.SendOnBufferingEnded();
            }
            else
            {
                IsBuffering = false;
            }
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
                GuessedByteRate = (ulong)(1.2 * bytesReadSoFar / shortestDuration.TotalSeconds);
                BufferCacheLength = Convert.ToInt32(GuessedByteRate);
                DownloadCacheLength = BufferCacheLength * (IsNetowrkStream ? NetworkStreamCacheFactor : StandardStreamCacheFactor);
            }
        }
    }
}
