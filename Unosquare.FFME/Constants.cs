namespace Unosquare.FFME
{
    using ClosedCaptions;
    using Engine;
    using FFmpeg.AutoGen;
    using System;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Defaults and constants of the Media Engine
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// Initializes static members of the <see cref="Constants"/> class.
        /// </summary>
        static Constants()
        {
            var entryAssemblyPath = ".";
            try
            {
                entryAssemblyPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) ?? ".";
            }
            catch
            {
                // ignore (we might be in winforms degin time)
                // see issue #311
            }

            FFmpegSearchPath = Path.GetFullPath(entryAssemblyPath);
        }

        /// <summary>
        /// Gets the assembly location.
        /// </summary>
        public static string FFmpegSearchPath { get; }

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

        /// <summary>
        /// The audio buffer padding
        /// </summary>
        public static int AudioBufferPadding => 256;

        /// <summary>
        /// The audio bits per sample (1 channel only)
        /// </summary>
        public static int AudioBitsPerSample => 16;

        /// <summary>
        /// The audio bytes per sample
        /// </summary>
        public static int AudioBytesPerSample => AudioBitsPerSample / 8;

        /// <summary>
        /// The audio sample format
        /// </summary>
        public static AVSampleFormat AudioSampleFormat => AVSampleFormat.AV_SAMPLE_FMT_S16;

        /// <summary>
        /// The audio channel count
        /// </summary>
        public static int AudioChannelCount => 2;

        /// <summary>
        /// The audio sample rate (per channel)
        /// </summary>
        public static int AudioSampleRate => 48000;

        /// <summary>
        /// The video bits per component
        /// </summary>
        public static int VideoBitsPerComponent => 8;

        /// <summary>
        /// The video bits per pixel
        /// </summary>
        public static int VideoBitsPerPixel => 32;

        /// <summary>
        /// The video bytes per pixel
        /// </summary>
        public static int VideoBytesPerPixel => 4;

        /// <summary>
        /// The video pixel format. BGRA, 32bit
        /// </summary>
        public static AVPixelFormat VideoPixelFormat => AVPixelFormat.AV_PIX_FMT_BGRA;

        /// <summary>
        /// Gets the time synchronize maximum offset.
        /// Components that are offset more than this time span with respect to the
        /// main component are deemed unrelated.
        /// </summary>
        internal static TimeSpan TimeSyncMaxOffset { get; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets the thread worker period.
        /// </summary>
        internal static TimeSpan ThreadWorkerPeriod => TimeSpan.FromMilliseconds(5);

        /// <summary>
        /// Gets the maximum blocks to cache for the given component type.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <param name="mediaCore">The media core.</param>
        /// <returns>The number of blocks to cache</returns>
        internal static int GetMaxBlocks(MediaType t, MediaEngine mediaCore)
        {
            const int MinVideoBlocks = 8;
            const int MinAudioBlocks = 48;
            const int MinSubtitleBlocks = 4;

            var result = 0;

            if (t == MediaType.Video)
            {
                result = mediaCore.MediaOptions.VideoBlockCache;
                if (result < MinVideoBlocks) result = MinVideoBlocks;
            }
            else if (t == MediaType.Audio)
            {
                result = mediaCore.MediaOptions.AudioBlockCache;
                if (result < MinAudioBlocks) result = MinAudioBlocks;
            }
            else if (t == MediaType.Subtitle)
            {
                result = mediaCore.MediaOptions.SubtitleBlockCache;
                if (result < MinSubtitleBlocks) result = MinSubtitleBlocks;
            }

            return result;
        }
    }
}
