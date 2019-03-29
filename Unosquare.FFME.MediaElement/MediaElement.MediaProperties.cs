namespace Unosquare.FFME
{
    using Common;
    using System;
    using System.Collections.ObjectModel;

    public partial class MediaElement
    {
        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        public MediaPlaybackState MediaState => MediaCore?.State.MediaState ?? MediaPlaybackState.Close;

        /// <summary>
        /// Gets the Media's natural duration
        /// Only valid after the MediaOpened event has fired.
        /// Returns null for undefined or negative duration
        /// </summary>
        public TimeSpan? NaturalDuration
        {
            get
            {
                var duration = MediaCore?.State.NaturalDuration;

                return !duration.HasValue || duration.Value < TimeSpan.Zero
                  ? default
                  : duration.Value;
            }
        }

        /// <summary>
        /// Gets the media playback start time. Useful for slider minimum values.
        /// </summary>
        public TimeSpan? PlaybackStartTime => MediaCore?.State.PlaybackStartTime;

        /// <summary>
        /// Gets the media playback end time. Useful for slider maximum values.
        /// </summary>
        public TimeSpan? PlaybackEndTime => MediaCore?.State.PlaybackEndTime;

        /// <summary>
        /// Gets the remaining playback duration. Returns null for indeterminate values.
        /// </summary>
        public TimeSpan? RemainingDuration
        {
            get
            {
                if (PlaybackEndTime.HasValue == false || IsSeekable == false) return default;
                return PlaybackEndTime.Value.Ticks < Position.Ticks
                    ? TimeSpan.Zero
                    : TimeSpan.FromTicks(PlaybackEndTime.Value.Ticks - Position.Ticks);
            }
        }

        /// <summary>
        /// Gets the discrete time position of the start of the current
        /// frame of the main component.
        /// </summary>
        public TimeSpan FramePosition => MediaCore?.State.FramePosition ?? default;

        /// <summary>
        /// Gets the stream's total bit rate as reported by the container.
        /// Returns 0 if unavailable.
        /// </summary>
        public long BitRate => MediaCore?.State.BitRate ?? default;

        /// <summary>
        /// Gets the instantaneous, compressed bit rate of the decoders for the currently active component streams.
        /// This is provided in bits per second.
        /// </summary>
        public long DecodingBitRate => MediaCore?.State.DecodingBitRate ?? default;

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
        /// Gets the index of the video stream.
        /// </summary>
        public int VideoStreamIndex => MediaCore?.State.VideoStreamIndex ?? -1;

        /// <summary>
        /// Gets the index of the audio stream.
        /// </summary>
        public int AudioStreamIndex => MediaCore?.State.AudioStreamIndex ?? -1;

        /// <summary>
        /// Gets the index of the subtitle stream.
        /// </summary>
        public int SubtitleStreamIndex => MediaCore?.State.SubtitleStreamIndex ?? -1;

        /// <summary>
        /// Gets the media format. Returns null when media has not been loaded.
        /// </summary>
        public string MediaFormat => MediaCore?.State.MediaFormat;

        /// <summary>
        /// Gets the size in bytes of the current stream being read.
        /// For multi-file streams, get the size of the current file only.
        /// </summary>
        public long MediaStreamSize => MediaCore?.State.MediaStreamSize ?? 0;

        /// <summary>
        /// Gets the duration of a single frame step.
        /// If there is a video component with a frame rate, this property returns the length of a frame.
        /// If there is no video component it simply returns a tenth of a second.
        /// </summary>
        public TimeSpan PositionStep => MediaCore?.State.PositionStep ?? TimeSpan.Zero;

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
        /// Gets the video bit rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public long VideoBitRate => MediaCore?.State.VideoBitRate ?? default;

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
        /// Gets the audio bit rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public long AudioBitRate => MediaCore?.State.AudioBitRate ?? default;

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
        /// Note that this property is computed based on whether the stream is detected to be a live stream.
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
        public bool IsNetworkStream => MediaCore?.State.IsNetworkStream ?? default;

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
        /// Returns the current video SMTPE time code if available.
        /// </summary>
        public string VideoSmtpeTimeCode => MediaCore?.State.VideoSmtpeTimeCode;

        /// <summary>
        /// Gets the current video aspect ratio if available.
        /// </summary>
        public string VideoAspectRatio => MediaCore?.State.VideoAspectRatio;

        /// <summary>
        /// Gets a value that indicates the percentage of buffering progress made.
        /// Range is from 0 to 1.
        /// </summary>
        public double BufferingProgress => MediaCore?.State.BufferingProgress ?? default;

        /// <summary>
        /// Gets a value that indicates the percentage of download progress made.
        /// Range is from 0 to 1.
        /// </summary>
        public double DownloadProgress => MediaCore?.State.DownloadProgress ?? default;

        /// <summary>
        /// Gets the amount of bytes in the packet buffer for the active stream components.
        /// </summary>
        public long PacketBufferLength => MediaCore?.State.PacketBufferLength ?? default;

        /// <summary>
        /// Gets the number of packets buffered for all components.
        /// </summary>
        public int PacketBufferCount => MediaCore?.State.PacketBufferCount ?? default;

        /// <summary>
        /// Gets a value indicating whether the media is in the process of opening.
        /// </summary>
        public bool IsOpening => MediaCore?.State.IsOpening ?? default;

        /// <summary>
        /// Gets a value indicating whether the media is in the process of closing.
        /// </summary>
        public bool IsClosing => MediaCore?.State.IsClosing ?? default;

        /// <summary>
        /// Gets a value indicating whether the media is currently changing its components.
        /// </summary>
        public bool IsChanging => MediaCore?.State.IsChanging ?? default;

        /// <summary>
        /// Gets a value indicating whether this media element
        /// currently has an open media url.
        /// </summary>
        public bool IsOpen => MediaCore?.State.IsOpen ?? default;

        /// <summary>
        /// Gets a value indicating whether the video stream contains closed captions.
        /// </summary>
        public bool HasClosedCaptions => MediaCore?.State.HasClosedCaptions ?? default;
    }
}
