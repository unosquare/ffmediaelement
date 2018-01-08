namespace Unosquare.FFME
{
    using System;
    using System.Collections.ObjectModel;
    using System.Windows;
    using System.Windows.Controls;

    public partial class MediaElement
    {
        #region Notification Properties

        /// <summary>
        /// Provides key-value pairs of the metadata contained in the media.
        /// Returns null when media has not been loaded.
        /// </summary>
        public ReadOnlyDictionary<string, string> Metadata => MediaCore?.Metadata;

        /// <summary>
        /// Gets the media format. Returns null when media has not been loaded.
        /// </summary>
        public string MediaFormat => MediaCore.MediaFormat;

        /// <summary>
        /// Gets the duration of a single frame step.
        /// If there is a video component with a framerate, this propery returns the length of a frame.
        /// If there is no video component it simply returns a tenth of a second.
        /// </summary>
        public TimeSpan FrameStepDuration => MediaCore.FrameStepDuration;

        /// <summary> 
        /// Returns whether the given media has audio. 
        /// Only valid after the MediaOpened event has fired.
        /// </summary> 
        public bool HasAudio => MediaCore.HasAudio;

        /// <summary> 
        /// Returns whether the given media has video. Only valid after the
        /// MediaOpened event has fired.
        /// </summary>
        public bool HasVideo => MediaCore.HasVideo;

        /// <summary>
        /// Gets the video codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string VideoCodec => MediaCore.VideoCodec;

        /// <summary>
        /// Gets the video bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int VideoBitrate => MediaCore.VideoBitrate;

        /// <summary>
        /// Returns the natural width of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary> 
        public int NaturalVideoWidth => MediaCore.NaturalVideoWidth;

        /// <summary> 
        /// Returns the natural height of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoHeight => MediaCore.NaturalVideoHeight;

        /// <summary>
        /// Gets the video frame rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameRate => MediaCore.VideoFrameRate;

        /// <summary>
        /// Gets the duration in seconds of the video frame.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameLength => MediaCore.VideoFrameLength;

        /// <summary>
        /// Gets the name of the video hardware decoder in use.
        /// Enabling hardware acceleration does not guarantee decoding will be performed in hardware.
        /// When hardware decoding of frames is in use this will return the name of the HW accelerator.
        /// Otherwise it will return an empty string.
        /// </summary>
        public string VideoHardwareDecoder => MediaCore.VideoHardwareDecoder;

        /// <summary>
        /// Gets the audio codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string AudioCodec => MediaCore.AudioCodec;

        /// <summary>
        /// Gets the audio bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitrate => MediaCore.AudioBitrate;

        /// <summary>
        /// Gets the audio channels count.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioChannels => MediaCore.AudioChannels;

        /// <summary>
        /// Gets the audio sample rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioSampleRate => MediaCore.AudioSampleRate;

        /// <summary>
        /// Gets the audio bits per sample.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitsPerSample => MediaCore.AudioBitsPerSample;

        /// <summary>
        /// Gets the Media's natural duration
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public Duration NaturalDuration
        {
            get
            {
                return MediaCore.NaturalDuration == null
                  ? Duration.Automatic
                  : (MediaCore.NaturalDuration.Value == TimeSpan.MinValue
                    ? Duration.Forever
                    : (MediaCore.NaturalDuration.Value < TimeSpan.Zero
                    ? default(Duration)
                    : new Duration(MediaCore.NaturalDuration.Value)));
            }
        }

        /// <summary>
        /// Returns whether the currently loaded media can be paused.
        /// This is only valid after the MediaOpened event has fired.
        /// Note that this property is computed based on wether the stream is detected to be a live stream.
        /// </summary>
        public bool CanPause => MediaCore.CanPause;

        /// <summary>
        /// Returns whether the currently loaded media is live or realtime
        /// This is only valid after the MediaOpened event has fired.
        /// </summary>
        public bool IsLiveStream => MediaCore.IsLiveStream;

        /// <summary>
        /// Gets a value indicating whether the currently loaded media can be seeked.
        /// </summary>
        public bool IsSeekable => MediaCore.IsSeekable;

        /// <summary>
        /// Gets a value indicating whether the media is playing.
        /// </summary>
        public bool IsPlaying => MediaCore.IsPlaying;

        /// <summary>
        /// Gets a value indicating whether the media has reached its end.
        /// </summary>
        public bool HasMediaEnded => MediaCore.HasMediaEnded;

        /// <summary>
        /// Get a value indicating whether the media is buffering.
        /// </summary>
        public bool IsBuffering => MediaCore.IsBuffering;

        /// <summary>
        /// Gets a value indicating whether the media seeking is in progress.
        /// </summary>
        public bool IsSeeking => MediaCore.IsSeeking;

        /// <summary>
        /// Returns the current video SMTPE timecode if available.
        /// If not available, this property returns an empty string.
        /// </summary>
        public string VideoSmtpeTimecode => MediaCore.VideoSmtpeTimecode;

        /// <summary>
        /// Gets a value that indicates the percentage of buffering progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double BufferingProgress => MediaCore.BufferingProgress;

        /// <summary>
        /// The wait packet buffer length.
        /// It is adjusted to 1 second if bitrate information is available.
        /// Otherwise, it's simply 512KB
        /// </summary>
        public int BufferCacheLength => MediaCore.BufferCacheLength;

        /// <summary>
        /// Gets a value that indicates the percentage of download progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double DownloadProgress => MediaCore.DownloadProgress;

        /// <summary>
        /// Gets the maximum packet buffer length, according to the bitrate (if available).
        /// If it's a realtime stream it will return 30 times the buffer cache length.
        /// Otherwise, it will return  4 times of the buffer cache length.
        /// </summary>
        public int DownloadCacheLength => MediaCore.DownloadCacheLength;

        /// <summary>
        /// Gets a value indicating whether the media is in the process of opening.
        /// </summary>
        public bool IsOpening => MediaCore.IsOpening;

        /// <summary>
        /// Gets a value indicating whether this media element
        /// currently has an open media url.
        /// </summary>
        public bool IsOpen => MediaCore.IsOpen;

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        public MediaState MediaState => (MediaState)MediaCore.MediaState;

        #endregion
    }
}
