namespace Unosquare.FFME.Shared
{
    using System.Collections.Generic;

    /// <summary>
    /// Represetnts options that applied creating the individual media stream components.
    /// Once the container has created the media components, changing these options will have no effect.
    /// See: https://www.ffmpeg.org/ffmpeg-all.html#Main-options
    /// Partly a port of https://github.com/FFmpeg/FFmpeg/blob/master/fftools/ffmpeg_opt.c
    /// </summary>
    public sealed class MediaOptions
    {
        internal MediaOptions()
        {
            // placeholder
        }

        /// <summary>
        /// Provides access to the global and per-stream decoder options
        /// See https://www.ffmpeg.org/ffmpeg-codecs.html#Codec-Options
        /// </summary>
        public DecoderOptions DecoderParams { get; } = new DecoderOptions();

        /// <summary>
        /// A dictionary of stream indexes and force decoder codec names.
        /// This is equivalent to the -codec Main option.
        /// See: https://www.ffmpeg.org/ffmpeg-all.html#Main-options (-codec option)
        /// </summary>
        public Dictionary<int, string> DecoderCodec { get; } = new Dictionary<int, string>();

        /// <summary>
        /// Use Stream's HardwareDevices property to get a list of
        /// compatible hardware accelerators.
        /// </summary>
        public HardwareDeviceInfo VideoHardwareDevice { get; set; }

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
        /// Specifies a forced FPS value for the input video stream.
        /// </summary>
        public double VideoForcedFps { get; set; } = default;

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

        /// <summary>
        /// Gets or sets the subtitles URL.
        /// If set, the subtitles will be side-loaded and the loaded media
        /// subtitles (if any) will be ignored.
        /// </summary>
        public string SubtitlesUrl { get; set; } = null;
    }
}
