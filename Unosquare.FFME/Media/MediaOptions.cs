namespace Unosquare.FFME.Media
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents options that applied creating the individual media stream components.
    /// Once the container has created the media components, changing these options will have no effect.
    /// See: https://www.ffmpeg.org/ffmpeg-all.html#Main-options
    /// Partly a port of https://github.com/FFmpeg/FFmpeg/blob/master/fftools/ffmpeg_opt.c.
    /// </summary>
    public sealed class MediaOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaOptions"/> class.
        /// </summary>
        internal MediaOptions()
        {
            // placeholder
        }

        /// <summary>
        /// Provides access to the global and per-stream decoder options
        /// See https://www.ffmpeg.org/ffmpeg-codecs.html#Codec-Options.
        /// </summary>
        public DecoderOptions DecoderParams { get; } = new DecoderOptions();

        /// <summary>
        /// A dictionary of stream indexes and force decoder codec names.
        /// This is equivalent to the -codec Main option.
        /// See: https://www.ffmpeg.org/ffmpeg-all.html#Main-options (-codec option).
        /// </summary>
        public Dictionary<int, string> DecoderCodec { get; } = new Dictionary<int, string>(32);

        /// <summary>
        /// Gets or sets the amount of time to offset the subtitles by
        /// This is an FFME-only property -- Not a port of ffmpeg.
        /// TODO: Consider moving this property to RendererOptions.
        /// </summary>
        public TimeSpan SubtitlesDelay { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Use Stream's HardwareDevices property to get a list of
        /// compatible hardware accelerators.
        /// </summary>
        public HardwareDeviceInfo VideoHardwareDevice { get; set; }

        /// <summary>
        /// Prevent reading from audio stream components.
        /// Port of audio_disable.
        /// </summary>
        public bool IsAudioDisabled { get; set; }

        /// <summary>
        /// Prevent reading from video stream components.
        /// Port of video_disable.
        /// </summary>
        public bool IsVideoDisabled { get; set; }

        /// <summary>
        /// Prevent reading from subtitle stream components.
        /// Port of subtitle_disable
        /// Subtitles are not yet first-class citizens in FFmpeg and
        /// this is why they are disabled by default.
        /// </summary>
        public bool IsSubtitleDisabled { get; set; }

        /// <summary>
        /// Allows for a custom video filter string.
        /// Please see: https://ffmpeg.org/ffmpeg-filters.html#Video-Filters.
        /// </summary>
        public string VideoFilter { get; set; } = string.Empty;

        /// <summary>
        /// Specifies a forced FPS value for the input video stream.
        /// </summary>
        public double VideoForcedFps { get; set; }

        /// <summary>
        /// Initially contains the best suitable video stream.
        /// Can be changed to a different stream reference.
        /// </summary>
        public StreamInfo VideoStream { get; set; }

        /// <summary>
        /// Gets or sets the video seek index.
        /// Use <see cref="MediaElement.CreateVideoSeekIndex"/> and set this
        /// field while loading the options.
        /// </summary>
        public VideoSeekIndex VideoSeekIndex { get; set; }

        /// <summary>
        /// Allows for a custom audio filter string.
        /// Please see: https://ffmpeg.org/ffmpeg-filters.html#Audio-Filters.
        /// </summary>
        public string AudioFilter { get; set; } = string.Empty;

        /// <summary>
        /// Initially contains the best suitable audio stream.
        /// Can be changed to a different stream reference.
        /// </summary>
        public StreamInfo AudioStream { get; set; }

        /// <summary>
        /// Initially contains the best suitable subtitle stream.
        /// Can be changed to a different stream reference.
        /// </summary>
        public StreamInfo SubtitleStream { get; set; }

        /// <summary>
        /// Gets or sets the subtitles URL.
        /// If set, the subtitles will be side-loaded and the loaded media
        /// subtitles (if any) will be ignored.
        /// </summary>
        public string SubtitlesSource { get; set; }

        /// <summary>
        /// Gets or sets the number of video blocks to cache in the decoder.
        /// Leave as -1 for auto. Please note that increasing the amount of
        /// blocks, significantly increases RAM usage.
        /// </summary>
        public int VideoBlockCache { get; set; } = -1;

        /// <summary>
        /// Gets or sets the number of audio blocks to cache in the decoder.
        /// Leave as -1 for auto. Please note that increasing the amount of
        /// blocks, significantly increases RAM usage.
        /// </summary>
        public int AudioBlockCache { get; set; } = -1;

        /// <summary>
        /// Gets or sets the number of audio blocks to cache in the decoder.
        /// Leave as -1 for auto. Please note that increasing the amount of
        /// blocks, significantly increases RAM usage.
        /// </summary>
        public int SubtitleBlockCache { get; set; } = -1;

        /// <summary>
        /// Only recommended for live streams. Gets or sets a value indicating whether each component needs to run
        /// its timing independently. This property is useful when for example when
        /// the audio and the video components of the stream have no timing relationship or when you don't need the
        /// components to be synchronized between them.
        /// </summary>
        public bool IsTimeSyncDisabled { get; set; }

        /// <summary>
        /// Typically, and especially for live streams, you want to wait for some packet data to become available before resuming
        /// playback. The buffer percent represents 1 second of data for each component -- excluding subtitles.
        /// Valid range is from 0.0 (do not wait for packet data) to 1.0 (wait for 1 second on each component.
        /// This defaults to 0.5 which is suitable for most live streams.
        /// </summary>
        public double MinimumPlaybackBufferPercent { get; set; } = 0.5;

        /// <summary>
        /// Gets or sets a value indicating whether component frames are decoded in
        /// parallel. This defaults to false but it is enabled automatically when <see cref="IsTimeSyncDisabled"/> is enabled.
        /// </summary>
        public bool UseParallelDecoding { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether component blocks are sent to their corresponding
        /// renderers in parallel. This defaults to false.
        /// </summary>
        public bool UseParallelRendering { get; set; }
    }
}
