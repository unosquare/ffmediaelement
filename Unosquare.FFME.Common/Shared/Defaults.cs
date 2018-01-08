namespace Unosquare.FFME.Shared
{
    using FFmpeg.AutoGen;
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Defaults and constants of the Media Engine
    /// </summary>
    public static class Defaults
    {
        #region Controller Value Defaults

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

        #endregion

        #region Decoder Constants

        /// <summary>
        /// The audio buffer padding
        /// </summary>
        public const int AudioBufferPadding = 256;

        /// <summary>
        /// The audio bits per sample (1 channel only)
        /// </summary>
        public const int AudioBitsPerSample = 16;

        /// <summary>
        /// The audio sample format
        /// </summary>
        public const AVSampleFormat AudioSampleFormat = AVSampleFormat.AV_SAMPLE_FMT_S16;

        /// <summary>
        /// The audio channel count
        /// </summary>
        public const int AudioChannelCount = 2;

        /// <summary>
        /// The audio sample rate (per channel)
        /// </summary>
        public const int AudioSampleRate = 48000;

        /// <summary>
        /// The video bits per component
        /// </summary>
        public const int VideoBitsPerComponent = 8;

        /// <summary>
        /// The video bits per pixel
        /// </summary>
        public const int VideoBitsPerPixel = 32;

        /// <summary>
        /// The video bytes per pixel
        /// </summary>
        public const int VideoBytesPerPixel = 4;

        /// <summary>
        /// The video pixel format. BGRX, 32bit
        /// </summary>
        public const AVPixelFormat VideoPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR0;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the assembly location.
        /// </summary>
        public static string EntryAssemblyPath { get; } = Path.GetFullPath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));

        /// <summary>
        /// Gets the architecture C.
        /// </summary>
        public static Architecture Architecture { get; } = RuntimeInformation.ProcessArchitecture;

        /// <summary>
        /// The timer high priority interval for stuff like rendering
        /// </summary>
        public static TimeSpan TimerHighPriorityInterval { get; } = TimeSpan.FromMilliseconds(15);

        /// <summary>
        /// The timer medium priority interval for stuff like property updates
        /// </summary>
        public static TimeSpan TimerMediumPriorityInterval { get; } = TimeSpan.FromMilliseconds(25);

        /// <summary>
        /// The timer low priority interval for stuff like logging
        /// </summary>
        public static TimeSpan TimerLowPriorityInterval { get; } = TimeSpan.FromMilliseconds(40);

        #endregion
    }
}
