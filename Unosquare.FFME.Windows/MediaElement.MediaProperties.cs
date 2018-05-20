namespace Unosquare.FFME
{
    using Rendering;
    using Shared;
    using System;
    using System.Collections.ObjectModel;
    using System.Windows;
    using System.Windows.Controls;

    public partial class MediaElement
    {
        /// <summary>
        /// Provides access to various internal media renderer options.
        /// The default options are optimal to work for most media streams.
        /// This is an advanced feature and it is not recommended to change these
        /// options without careful consideration.
        /// </summary>
        public RendererOptions RendererOptions { get; } = new RendererOptions();

        /// <summary>
        /// Gets the Media's natural duration
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public Duration NaturalDuration
        {
            get
            {
                var duration = MediaCore?.State.NaturalDuration;

                return !duration.HasValue
                  ? Duration.Automatic
                  : (duration.Value == TimeSpan.MinValue
                    ? Duration.Forever
                    : (duration.Value < TimeSpan.Zero
                    ? default
                    : new Duration(duration.Value)));
            }
        }

        /// <summary>
        /// Gets the remaining playback duration. Returns Forever for indeterminate values.
        /// </summary>
        public Duration RemainingDuration
        {
            get
            {
                if (NaturalDuration.HasTimeSpan == false) return Duration.Forever;
                if (NaturalDuration.TimeSpan.Ticks < Position.Ticks) return new Duration(NaturalDuration.TimeSpan);
                return new Duration(TimeSpan.FromTicks(NaturalDuration.TimeSpan.Ticks - Position.Ticks));
            }
        }

        /// <summary>
        /// Provides key-value pairs of the metadata contained in the media.
        /// Returns null when media has not been loaded.
        /// </summary>
        public ReadOnlyDictionary<string, string> Metadata => MediaCore?.State.Metadata;

        /// <summary>
        /// Provides stream, chapter and program info of the underlying media.
        /// Returns null when no media is loaded.
        /// </summary>
        public MediaInfo MediaInfo => MediaCore?.MediaInfo;

        /// <summary>
        /// Gets the media format. Returns null when media has not been loaded.
        /// </summary>
        public string MediaFormat => MediaCore?.State.MediaFormat;

        /// <summary>
        /// Gets the duration of a single frame step.
        /// If there is a video component with a framerate, this propery returns the length of a frame.
        /// If there is no video component it simply returns a tenth of a second.
        /// </summary>
        public TimeSpan FrameStepDuration => MediaCore?.State.FrameStepDuration ?? TimeSpan.Zero;

        /// <summary>
        /// Returns whether the given media has audio.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public bool HasAudio => MediaCore?.State.HasAudio ?? default;

        /// <summary>
        /// Returns whether the given media has video. Only valid after the
        /// MediaOpened event has fired.
        /// </summary>
        public bool HasVideo => MediaCore?.State.HasVideo ?? default;

        /// <summary>
        /// Returns whether the given media has subtitles. Only valid after the
        /// MediaOpened event has fired.
        /// </summary>
        public bool HasSubtitles => MediaCore?.State.HasSubtitles ?? false;

        /// <summary>
        /// Gets the video codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string VideoCodec => MediaCore?.State.VideoCodec;

        /// <summary>
        /// Gets the video bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public ulong VideoBitrate => MediaCore?.State.VideoBitrate ?? default;

        /// <summary>
        /// Returns the clockwise angle that needs to be applied to the video for it to be displayed
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoRotation => MediaCore?.State.VideoRotation ?? default;

        /// <summary>
        /// Returns the natural width of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoWidth => MediaCore?.State.NaturalVideoWidth ?? default;

        /// <summary>
        /// Returns the natural height of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoHeight => MediaCore?.State.NaturalVideoHeight ?? default;

        /// <summary>
        /// Gets the video frame rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameRate => MediaCore?.State.VideoFrameRate ?? default;

        /// <summary>
        /// Gets the duration in seconds of the video frame.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameLength => MediaCore?.State.VideoFrameLength ?? default;

        /// <summary>
        /// Gets the name of the video hardware decoder in use.
        /// Enabling hardware acceleration does not guarantee decoding will be performed in hardware.
        /// When hardware decoding of frames is in use this will return the name of the HW accelerator.
        /// Otherwise it will return an empty string.
        /// </summary>
        public string VideoHardwareDecoder => MediaCore?.State.VideoHardwareDecoder;

        /// <summary>
        /// Gets the audio codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string AudioCodec => MediaCore?.State.AudioCodec;

        /// <summary>
        /// Gets the audio bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public ulong AudioBitrate => MediaCore?.State.AudioBitrate ?? default;

        /// <summary>
        /// Gets the audio channels count.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioChannels => MediaCore?.State.AudioChannels ?? default;

        /// <summary>
        /// Gets the audio sample rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioSampleRate => MediaCore?.State.AudioSampleRate ?? default;

        /// <summary>
        /// Gets the audio bits per sample.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitsPerSample => MediaCore?.State.AudioBitsPerSample ?? default;

        /// <summary>
        /// Returns whether the currently loaded media can be paused.
        /// This is only valid after the MediaOpened event has fired.
        /// Note that this property is computed based on wether the stream is detected to be a live stream.
        /// </summary>
        public bool CanPause => MediaCore?.State.CanPause ?? default;

        /// <summary>
        /// Returns whether the currently loaded media is live or realtime
        /// This is only valid after the MediaOpened event has fired.
        /// </summary>
        public bool IsLiveStream => MediaCore?.State.IsLiveStream ?? default;

        /// <summary>
        /// Returns whether the currently loaded media is a network stream.
        /// This is only valid after the MediaOpened event has fired.
        /// </summary>
        public bool IsNetowrkStream => MediaCore?.State.IsNetowrkStream ?? default;

        /// <summary>
        /// Gets a value indicating whether the currently loaded media can be seeked.
        /// </summary>
        public bool IsSeekable => MediaCore?.State.IsSeekable ?? default;

        /// <summary>
        /// Gets a value indicating whether the media is playing.
        /// </summary>
        public bool IsPlaying => MediaCore?.State.IsPlaying ?? default;

        /// <summary>
        /// Gets a value indicating whether the media is playing.
        /// </summary>
        public bool IsPaused => MediaCore?.State.IsPaused ?? default;

        /// <summary>
        /// Gets a value indicating whether the media has reached its end.
        /// </summary>
        public bool HasMediaEnded => MediaCore?.State.HasMediaEnded ?? default;

        /// <summary>
        /// Get a value indicating whether the media is buffering.
        /// </summary>
        public bool IsBuffering => MediaCore?.State.IsBuffering ?? default;

        /// <summary>
        /// Gets a value indicating whether the media seeking is in progress.
        /// </summary>
        public bool IsSeeking => MediaCore?.State.IsSeeking ?? default;

        /// <summary>
        /// Returns the current video SMTPE timecode if available.
        /// If not available, this property returns an empty string.
        /// </summary>
        public string VideoSmtpeTimecode => MediaCore?.State.VideoSmtpeTimecode;

        /// <summary>
        /// Gets a value that indicates the percentage of buffering progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double BufferingProgress => MediaCore?.State.BufferingProgress ?? default;

        /// <summary>
        /// The wait packet buffer length.
        /// It is adjusted to 1 second if bitrate information is available.
        /// Otherwise, it's simply 512KB
        /// </summary>
        public ulong BufferCacheLength => MediaCore?.State.BufferCacheLength ?? default;

        /// <summary>
        /// Gets a value that indicates the percentage of download progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double DownloadProgress => MediaCore?.State.DownloadProgress ?? default;

        /// <summary>
        /// Gets the maximum packet buffer length, according to the bitrate (if available).
        /// If it's a realtime stream it will return 30 times the buffer cache length.
        /// Otherwise, it will return  4 times of the buffer cache length.
        /// </summary>
        public ulong DownloadCacheLength => MediaCore?.State.DownloadCacheLength ?? default;

        /// <summary>
        /// Gets a value indicating whether the media is in the process of opening.
        /// </summary>
        public bool IsOpening => MediaCore?.State.IsOpening ?? default;

        /// <summary>
        /// Gets a value indicating whether this media element
        /// currently has an open media url.
        /// </summary>
        public bool IsOpen => MediaCore?.State.IsOpen ?? default;

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        public MediaState MediaState => (MediaState)(MediaCore?.State.MediaState ?? PlaybackStatus.Close);
    }
}
