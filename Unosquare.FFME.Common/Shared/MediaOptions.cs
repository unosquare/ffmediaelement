namespace Unosquare.FFME.Shared
{
    /// <summary>
    /// Represetnts options that applied before initializing media components and their corresponding
    /// codecs. Once the container has created the media components, changing these options will have no effect.
    /// </summary>
    public sealed class MediaOptions
    {
        internal MediaOptions()
        {
            // placeholder
        }

        /// <summary>
        /// Gets the codec options.
        /// Codec options are documented here: https://www.ffmpeg.org/ffmpeg-codecs.html#Codec-Options
        /// Port of codec_opts
        /// </summary>
        public MediaCodecOptions CodecOptions { get; } = new MediaCodecOptions();

        /// <summary>
        /// Gets or sets a value indicating whether [enable low resource].
        /// In theroy this should be 0,1,2,3 for 1, 1/2, 1,4 and 1/8 resolutions.
        /// TODO: We are for now just supporting 1/2 resolution (true value)
        /// Port of lowres.
        /// </summary>
        public bool EnableLowRes { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether [enable fast decoding].
        /// Port of fast
        /// </summary>
        public bool EnableFastDecoding { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether experimental hardware acceleration is enabled.
        /// Defaults to false. This feature is experimental.
        /// </summary>
        public bool EnableHardwareAcceleration { get; set; }

        /// <summary>
        /// Prevent reading from audio stream components.
        /// Port of audio_disable
        /// </summary>
        public bool IsAudioDisabled { get; set; } = false;

        /// <summary>
        /// Prevent reading from video stream components.
        /// Port of video_disable
        /// </summary>
        public bool IsVideoDisabled { get; set; } = false;

        /// <summary>
        /// Prevent reading from subtitle stream components.
        /// Port of subtitle_disable
        /// Subtitles are not yet first-class citizens in FFmpeg and
        /// this is why they are disabled by default.
        /// </summary>
        public bool IsSubtitleDisabled { get; set; } = false;

        /// <summary>
        /// Allows for a custom video filter string.
        /// Please see: https://ffmpeg.org/ffmpeg-filters.html#Video-Filters
        /// </summary>
        public string VideoFilter { get; set; } = string.Empty;

        /// <summary>
        /// Initially contains the best suitable video stream.
        /// Can be changed to a different stream reference.
        /// </summary>
        public StreamInfo VideoStream { get; set; } = null;

        /// <summary>
        /// Allows for a custom audio filter string.
        /// Please see: https://ffmpeg.org/ffmpeg-filters.html#Audio-Filters
        /// </summary>
        public string AudioFilter { get; set; } = string.Empty;

        /// <summary>
        /// Initially contains the best suitable audio stream.
        /// Can be changed to a different stream reference.
        /// </summary>
        public StreamInfo AudioStream { get; set; } = null;

        /// <summary>
        /// Initially contains the best suitable subititle stream.
        /// Can be changed to a different stream reference.
        /// </summary>
        public StreamInfo SubtitleStream { get; set; } = null;
    }
}
