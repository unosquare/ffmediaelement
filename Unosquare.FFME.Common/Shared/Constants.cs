namespace Unosquare.FFME.Shared
{
    using ClosedCaptions;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Defaults and constants of the Media Engine
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Initializes static members of the <see cref="Constants"/> class.
        /// </summary>
        static Constants()
        {
            FFmpegSearchPath = Path.GetFullPath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
        }

        /// <summary>
        /// Gets the assembly location.
        /// </summary>
        public static string FFmpegSearchPath { get; }

        // TODO: (Floyd) Make this configurable. Maybe part of Media Options? See frame caching policy issue #139.
        internal static Dictionary<MediaType, int> MaxBlocks { get; } = new Dictionary<MediaType, int>
        {
            { MediaType.Video, 12 },
            { MediaType.Audio, 120 },
            { MediaType.Subtitle, 120 }
        };

        /// <summary>
        /// Gets all media types in an array.
        /// </summary>
        internal static MediaType[] MediaTypes { get; } = new MediaType[] { MediaType.Video, MediaType.Audio, MediaType.Subtitle };

        /// <summary>
        /// Defines Controller Value Defaults
        /// </summary>
        public static class Controller
        {
            /// <summary>
            /// The default speed ratio
            /// </summary>
            public static double DefaultSpeedRatio => 1.0d;

            /// <summary>
            /// The default balance
            /// </summary>
            public static double DefaultBalance => 0.0d;

            /// <summary>
            /// The default volume
            /// </summary>
            public static double DefaultVolume => 1.0d;

            /// <summary>
            /// The default closed captions channel
            /// </summary>
            public static CaptionsChannel DefaultClosedCaptionsChannel => CaptionsChannel.CCP;

            /// <summary>
            /// The minimum speed ratio
            /// </summary>
            public static double MinSpeedRatio => 0.0d;

            /// <summary>
            /// The maximum speed ratio
            /// </summary>
            public static double MaxSpeedRatio => 8.0d;

            /// <summary>
            /// The minimum balance
            /// </summary>
            public static double MinBalance => -1.0d;

            /// <summary>
            /// The maximum balance
            /// </summary>
            public static double MaxBalance => 1.0d;

            /// <summary>
            /// The maximum volume
            /// </summary>
            public static double MaxVolume => 1.0d;

            /// <summary>
            /// The minimum volume
            /// </summary>
            public static double MinVolume => 0.0d;
        }

        /// <summary>
        /// Defines decoder output constants for audio streams
        /// </summary>
        public static class Audio
        {
            /// <summary>
            /// The audio buffer padding
            /// </summary>
            public static int BufferPadding => 256;

            /// <summary>
            /// The audio bits per sample (1 channel only)
            /// </summary>
            public static int BitsPerSample => 16;

            /// <summary>
            /// The audio bytes per sample
            /// </summary>
            public static int BytesPerSample => BitsPerSample / 8;

            /// <summary>
            /// The audio sample format
            /// </summary>
            public static AVSampleFormat SampleFormat => AVSampleFormat.AV_SAMPLE_FMT_S16;

            /// <summary>
            /// The audio channel count
            /// </summary>
            public static int ChannelCount => 2;

            /// <summary>
            /// The audio sample rate (per channel)
            /// </summary>
            public static int SampleRate => 48000;
        }

        /// <summary>
        /// Defines decoder output constants for audio streams
        /// </summary>
        public static class Video
        {
            /// <summary>
            /// The video bits per component
            /// </summary>
            public static int BitsPerComponent => 8;

            /// <summary>
            /// The video bits per pixel
            /// </summary>
            public static int BitsPerPixel => 32;

            /// <summary>
            /// The video bytes per pixel
            /// </summary>
            public static int BytesPerPixel => 4;

            /// <summary>
            /// The video pixel format. BGRA, 32bit
            /// </summary>
            public static AVPixelFormat VideoPixelFormat => AVPixelFormat.AV_PIX_FMT_BGRA;
        }

        /// <summary>
        /// Defines timespans of different priority intervals
        /// </summary>
        public static class Interval
        {
            /// <summary>
            /// The timer high priority interval for stuff like rendering
            /// </summary>
            public static TimeSpan HighPriority => TimeSpan.FromMilliseconds(15);

            /// <summary>
            /// The timer medium priority interval for stuff like property updates
            /// </summary>
            public static TimeSpan MediumPriority => TimeSpan.FromMilliseconds(25);

            /// <summary>
            /// The timer low priority interval for stuff like logging
            /// </summary>
            public static TimeSpan LowPriority => TimeSpan.FromMilliseconds(40);
        }
    }
}
