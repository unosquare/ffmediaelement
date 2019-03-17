// ReSharper disable UnusedMember.Global
namespace Unosquare.FFME.Engine
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Provides Media Engine state as read-only properties
    /// </summary>
    public interface IMediaEngineState
    {
        /// <summary>
        /// Gets the input bit rate as reported by the container. Returns 0 if unavailable.
        /// </summary>
        long BitRate { get; }

        /// <summary>
        /// Gets the instantaneous, compressed bit rate of the decoders for the currently active component streams.
        /// This is provided in bits per second.
        /// </summary>
        long DecodingBitRate { get; }

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
        /// Gets the audio bit rate.
        /// </summary>
        long AudioBitRate { get; }

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
        /// Gets the buffering progress.
        /// </summary>
        double BufferingProgress { get; }

        /// <summary>
        /// Gets a value indicating whether the media can pause.
        /// </summary>
        bool CanPause { get; }

        /// <summary>
        /// Gets the byte length of the buffered packets
        /// </summary>
        long PacketBufferLength { get; }

        /// <summary>
        /// Gets the number of packets buffered for all media components.
        /// </summary>
        int PacketBufferCount { get; }

        /// <summary>
        /// Gets the download progress.
        /// </summary>
        double DownloadProgress { get; }

        /// <summary>
        /// Gets the duration of a frame step.
        /// </summary>
        TimeSpan PositionStep { get; }

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
        /// Gets the playback start position of the media.
        /// </summary>
        TimeSpan? PlaybackStartTime { get; }

        /// <summary>
        /// Gets the playback end position of the media.
        /// </summary>
        TimeSpan? PlaybackEndTime { get; }

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
        /// Gets the discrete time position of the start of the current
        /// frame of the main component.
        /// </summary>
        TimeSpan FramePosition { get; }

        /// <summary>
        /// Gets the URL of the open or opening media.
        /// </summary>
        Uri Source { get; }

        /// <summary>
        /// Gets the playback speed ratio.
        /// </summary>
        double SpeedRatio { get; }

        /// <summary>
        /// Gets the video bit rate.
        /// </summary>
        long VideoBitRate { get; }

        /// <summary>
        /// Gets the video codec.
        /// </summary>
        string VideoCodec { get; }

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
        /// Gets the video SMTPE time code.
        /// </summary>
        string VideoSmtpeTimeCode { get; }

        /// <summary>
        /// Gets the current video aspect ratio.
        /// </summary>
        string VideoAspectRatio { get; }

        /// <summary>
        /// Gets the current audio volume.
        /// </summary>
        double Volume { get; }
    }
}