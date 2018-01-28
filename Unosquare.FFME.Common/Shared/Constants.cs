namespace Unosquare.FFME.Shared
{
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
        /// Gets the assembly location.
        /// </summary>
        public static string FFmpegSearchPath { get; } = Path.GetFullPath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));

        // TODO: Make this configurable
        internal static Dictionary<MediaType, int> MaxBlocks { get; } = new Dictionary<MediaType, int>
        {
            { MediaType.Video, 12 },
            { MediaType.Audio, 120 },
            { MediaType.Subtitle, 120 }
        };

        /// <summary>
        /// Defines Controller Value Defaults
        /// </summary>
        public static class Controller
        {
            /// <summary>
            /// The default speed ratio
            /// </summary>
            public const double DefaultSpeedRatio = 1.0d;

            /// <summary>
            /// The default balance
            /// </summary>
            public const double DefaultBalance = 0.0d;

            /// <summary>
            /// The default volume
            /// </summary>
            public const double DefaultVolume = 1.0d;

            /// <summary>
            /// The minimum speed ratio
            /// </summary>
            public const double MinSpeedRatio = 0.0d;

            /// <summary>
            /// The maximum speed ratio
            /// </summary>
            public const double MaxSpeedRatio = 8.0d;

            /// <summary>
            /// The minimum balance
            /// </summary>
            public const double MinBalance = -1.0d;

            /// <summary>
            /// The maximum balance
            /// </summary>
            public const double MaxBalance = 1.0d;

            /// <summary>
            /// The maximum volume
            /// </summary>
            public const double MaxVolume = 1.0d;

            /// <summary>
            /// The minimum volume
            /// </summary>
            public const double MinVolume = 0.0d;
        }

        /// <summary>
        /// Defines decoder output constants for audio streams
        /// </summary>
        public static class Audio
        {
            /// <summary>
            /// The audio buffer padding
            /// </summary>
            public const int BufferPadding = 256;

            /// <summary>
            /// The audio bits per sample (1 channel only)
            /// </summary>
            public const int BitsPerSample = 16;

            /// <summary>
            /// The audio bytes per sample
            /// </summary>
            public const int BytesPerSample = BitsPerSample / 8;

            /// <summary>
            /// The audio sample format
            /// </summary>
            public const AVSampleFormat SampleFormat = AVSampleFormat.AV_SAMPLE_FMT_S16;

            /// <summary>
            /// The audio channel count
            /// </summary>
            public const int ChannelCount = 2;

            /// <summary>
            /// The audio sample rate (per channel)
            /// </summary>
            public const int SampleRate = 48000;
        }

        /// <summary>
        /// Defines decoder output constants for audio streams
        /// </summary>
        public static class Video
        {
            /// <summary>
            /// The video bits per component
            /// </summary>
            public const int BitsPerComponent = 8;

            /// <summary>
            /// The video bits per pixel
            /// </summary>
            public const int BitsPerPixel = 32;

            /// <summary>
            /// The video bytes per pixel
            /// </summary>
            public const int BytesPerPixel = 4;

            /// <summary>
            /// The video pixel format. BGRX, 32bit
            /// </summary>
            public const AVPixelFormat VideoPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR0;
        }

        /// <summary>
        /// Defines timespans of different priority intervals
        /// </summary>
        public static class Interval
        {
            /// <summary>
            /// The timer high priority interval for stuff like rendering
            /// </summary>
            public static TimeSpan HighPriority { get; } = TimeSpan.FromMilliseconds(15);

            /// <summary>
            /// The timer medium priority interval for stuff like property updates
            /// </summary>
            public static TimeSpan MediumPriority { get; } = TimeSpan.FromMilliseconds(25);

            /// <summary>
            /// The timer low priority interval for stuff like logging
            /// </summary>
            public static TimeSpan LowPriority { get; } = TimeSpan.FromMilliseconds(40);
        }
    }
}
