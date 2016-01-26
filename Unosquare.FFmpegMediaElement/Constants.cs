namespace Unosquare.FFmpegMediaElement
{
    using FFmpeg.AutoGen;
    using System;

    /// <summary>
    /// Defines various constants used across all classes
    /// </summary>
    internal static class Constants
    {

        public const decimal DefaultSpeedRatio = 1.0M;

        public const decimal MaxVolume = 1.0M;
        public const decimal MinVolume = 0.0M;

        #region Nicer access to common FFmpeg Constants

        public const int EndOfFileErrorCode = -541478725;
        public const int SuccessCode = 0;
        public static readonly AVRational TimebaseRatio = new AVRational() { num = (int)ffmpeg.AV_TIME_BASE, den = 1 };

        public const decimal SeekThresholdSeconds = 0.05M;
        public const decimal SeekOffsetSeconds = SeekThresholdSeconds * 10M;

        #endregion

        #region Timeout Values

        public static readonly TimeSpan WaitForPlaybackReadyStateTimeout = TimeSpan.FromMilliseconds(2000);
        public static readonly TimeSpan FrameExtractorFillTimeout = TimeSpan.FromMilliseconds(5000);
        public const int FrameExtractorWaitMs = 500;
        public const int FrameExtractorSleepTime = 1;

        public const int VideoRenderTimerIntervalMillis = 10;
        public const int SeekPositionUpdateTimerIntervalMillis = 5;
        public const int SeekPositionUpdateTimeoutMillis = 20;

        #endregion

        #region Frame Garbage Collector Constants

        public const int FrameCollectorSleepTime = 1;
        public const int FrameCollectorReleaseInterval = 50;
        public const int FrameCollectorForcedReleaseFrameCount = 10;

        #endregion

        #region Audio and Video Output Constants

        public const int AudioRendererBufferCount = 5;
        public const int AudioRendererBufferMilliseconds = 20;
        public const AVPixelFormat VideoOutputPixelFormat = AVPixelFormat.PIX_FMT_BGR24;
        public const AVSampleFormat AudioOutputSampleFormat = AVSampleFormat.AV_SAMPLE_FMT_S16;
        public const int AudioOutputChannelCount = 2;

        #endregion
    }
}
