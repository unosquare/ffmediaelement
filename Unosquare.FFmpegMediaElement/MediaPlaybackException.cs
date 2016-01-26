namespace Unosquare.FFmpegMediaElement
{
    using System;

    /// <summary>
    /// Enumerates Media Playback error codes
    /// </summary>
    public enum MediaPlaybackErrorCode
    {
        WaitForPlaybackTimedOut = 0x241,
        FillFramesFailed = 0x534,
        SeekFailedCritical = 0x982,
        SeekFailedWillRetry = 0x981,
        SeekFailedFFmpeg = 0x980,
        LoadFramesFailedInFirstSegment = 0x641,
        LoadFramesFailedForCurrentPosition = 0x642,
        LoadFramesFailedCritical = 0x643,
        FrameExtractionLoopForcedPause = 0x441
    }

    internal class MediaPlaybackErrorSources
    {
        public const string InternalSeekInput = "InternalSeekInput";
        public const string InternalLoadFrames = "InternalLoadFrames";
        public const string WaitForPlaybackReadyState = "WaitForPlaybackReadyState";
        public const string InternalFillFamesCache = "InternalFillFamesCache";
        public const string ExtractMediaFramesContinuously = "ExtractMediaFramesContinuously";
    }

    /// <summary>
    /// Represents an error that occurs during media playback or during a seek operation.
    /// </summary>
    public class MediaPlaybackException : Exception
    {
        public string Component { get; private set; }
        public MediaPlaybackErrorCode ErrorCode { get; private set; }

        public MediaPlaybackException(string component, MediaPlaybackErrorCode errorCode, string message)
            : base(message)
        {
            this.Component = component;
            this.ErrorCode = errorCode;
        }
    }
}
