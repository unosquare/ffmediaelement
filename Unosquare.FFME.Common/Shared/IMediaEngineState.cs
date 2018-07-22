namespace Unosquare.FFME.Shared
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Provides Media Engine state as read-only properties
    /// </summary>
    public interface IMediaEngineState
    {
        /// <summary>
        /// Gets the input bitrate as reported by the container. Returns 0 if unavaliable.
        /// </summary>
        ulong Bitrate { get; }

        /// <summary>
        /// Gets the instantaneous, compressed bitrate of the decoders for the currently active component streams.
        /// This is provided in bits per second.
        /// </summary>
        ulong DecodingBitrate { get; }

        /// <summary>
        /// Gets the index of the video stream.
        /// </summary>
        int VideoStreamIndex { get; }

        /// <summary>
        /// Gets the index of the audio stream.
        /// </summary>
        int AudioStreamIndex { get; }

        /// <summary>
        /// Gets the index of the subtitle stream.
        /// </summary>
        int SubtitleStreamIndex { get; }

        /// <summary>
        /// Gets the audio bitrate.
        /// </summary>
        ulong AudioBitrate { get; }

        /// <summary>
        /// Gets the audio bits per sample.
        /// </summary>
        int AudioBitsPerSample { get; }

        /// <summary>
        /// Gets the audio channels.
        /// </summary>
        int AudioChannels { get; }

        /// <summary>
        /// Gets the audio codec.
        /// </summary>
        string AudioCodec { get; }

        /// <summary>
        /// Gets the audio sample rate.
        /// </summary>
        int AudioSampleRate { get; }

        /// <summary>
        /// Gets the audio balance.
        /// </summary>
        double Balance { get; }

        /// <summary>
        /// Gets the length of the buffer cache.
        /// </summary>
        ulong BufferCacheLength { get; }

        /// <summary>
        /// Gets the buffering progress.
        /// </summary>
        double BufferingProgress { get; }

        /// <summary>
        /// Gets a value indicating whether the media can pause.
        /// </summary>
        bool CanPause { get; }

        /// <summary>
        /// Gets the length of the download cache.
        /// </summary>
        ulong DownloadCacheLength { get; }

        /// <summary>
        /// Gets the download progress.
        /// </summary>
        double DownloadProgress { get; }

        /// <summary>
        /// Gets the duration of the frame step.
        /// </summary>
        TimeSpan FrameStepDuration { get; }

        /// <summary>
        /// Gets the discrete timestamp of the next frame.
        /// </summary>
        TimeSpan PositionNext { get; }

        /// <summary>
        /// Gets the discrete timestamp of the current frame.
        /// </summary>
        TimeSpan PositionCurrent { get; }

        /// <summary>
        /// Gets the discrete timestamp of the previous frame.
        /// </summary>
        TimeSpan PositionPrevious { get; }

        /// <summary>
        /// Gets a value indicating whether the media has audio.
        /// </summary>
        bool HasAudio { get; }

        /// <summary>
        /// Gets a value indicating whether the media has ended.
        /// </summary>
        bool HasMediaEnded { get; }

        /// <summary>
        /// Gets a value indicating whether the media has subtitles.
        /// </summary>
        bool HasSubtitles { get; }

        /// <summary>
        /// Gets a value indicating whether the media has video.
        /// </summary>
        bool HasVideo { get; }

        /// <summary>
        /// Gets a value indicating whether the current video stream has closed captions
        /// </summary>
        bool HasClosedCaptions { get; }

        /// <summary>
        /// Gets a value indicating whether the media is buffering.
        /// </summary>
        bool IsBuffering { get; }

        /// <summary>
        /// Gets a value indicating whether the media is a live stream.
        /// </summary>
        bool IsLiveStream { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the media is muted.
        /// </summary>
        bool IsMuted { get; set; }

        /// <summary>
        /// Gets a value indicating whether the media is a network stream.
        /// </summary>
        bool IsNetworkStream { get; }

        /// <summary>
        /// Gets a value indicating whether the media is currently open.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Gets a value indicating whether the media is currently opening.
        /// </summary>
        bool IsOpening { get; }

        /// <summary>
        /// Gets a value indicating whether the media is currently closing.
        /// </summary>
        bool IsClosing { get; }

        /// <summary>
        /// Gets a value indicating whether the media is currently changing its components.
        /// </summary>
        bool IsChanging { get; }

        /// <summary>
        /// Gets a value indicating whether the media is paused.
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Gets a value indicating whether the media is playing.
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Gets a value indicating whether the media is seekable.
        /// </summary>
        bool IsSeekable { get; }

        /// <summary>
        /// Gets a value indicating whether the media is currently seeking.
        /// </summary>
        bool IsSeeking { get; }

        /// <summary>
        /// Gets the media format.
        /// </summary>
        string MediaFormat { get; }

        /// <summary>
        /// Gets the size in bytes of the current stream being read.
        /// For multi-file streams, get the size of the current file only.
        /// </summary>
        long MediaStreamSize { get; }

        /// <summary>
        /// Gets the playback status of the media.
        /// </summary>
        PlaybackStatus MediaState { get; }

        /// <summary>
        /// Gets the media metadata such as title, language, etc.
        /// </summary>
        ReadOnlyDictionary<string, string> Metadata { get; }

        /// <summary>
        /// Gets the duration of the media.
        /// </summary>
        TimeSpan? NaturalDuration { get; }

        /// <summary>
        /// Gets the height of the video in pixels.
        /// </summary>
        int NaturalVideoHeight { get; }

        /// <summary>
        /// Gets the width of the video in pixels.
        /// </summary>
        int NaturalVideoWidth { get; }

        /// <summary>
        /// Gets the current position of the media.
        /// </summary>
        TimeSpan Position { get; }

        /// <summary>
        /// Gets the URL of the open or opening media.
        /// </summary>
        Uri Source { get; }

        /// <summary>
        /// Gets the playback speed ratio.
        /// </summary>
        double SpeedRatio { get; }

        /// <summary>
        /// Gets the video bitrate.
        /// </summary>
        ulong VideoBitrate { get; }

        /// <summary>
        /// Gets the video codec.
        /// </summary>
        string VideoCodec { get; }

        /// <summary>
        /// Gets the duration in seconds of video each video frame.
        /// </summary>
        double VideoFrameLength { get; }

        /// <summary>
        /// Gets the video frame rate.
        /// </summary>
        double VideoFrameRate { get; }

        /// <summary>
        /// Gets the video hardware decoder.
        /// </summary>
        string VideoHardwareDecoder { get; }

        /// <summary>
        /// Gets the video rotation.
        /// </summary>
        double VideoRotation { get; }

        /// <summary>
        /// Gets the video smtpe timecode.
        /// </summary>
        string VideoSmtpeTimecode { get; }

        /// <summary>
        /// Gets the current audio volume.
        /// </summary>
        double Volume { get; }
    }
}