namespace Unosquare.FFME
{
    using Common;
    using Container;
    using System;
    using System.Runtime.CompilerServices;

    public partial class MediaElement
    {
        #region Events

        /// <summary>
        /// Occurs when a logging message from the FFmpeg library has been received.
        /// This is shared across all instances of Media Elements.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public static event EventHandler<MediaLogMessageEventArgs> FFmpegMessageLogged;

        /// <summary>
        /// Occurs right before the video is presented on the screen.
        /// You can update the pixels on the bitmap before it is rendered on the screen.
        /// Or you could take a screen shot.
        /// Ensure you handle this very quickly as it runs on the UI thread.
        /// </summary>
        public event EventHandler<RenderingVideoEventArgs> RenderingVideo;

        /// <summary>
        /// Occurs right before the audio is added to the audio buffer.
        /// You can update the bytes before they are queued.
        /// Ensure you handle this quickly before you get choppy audio.
        /// </summary>
        public event EventHandler<RenderingAudioEventArgs> RenderingAudio;

        /// <summary>
        /// Occurs right before the subtitles are rendered.
        /// You can update the text.
        /// Ensure you handle this quickly before you get choppy subtitles.
        /// </summary>
        public event EventHandler<RenderingSubtitlesEventArgs> RenderingSubtitles;

        /// <summary>
        /// Occurs right before the data are rendered.
        /// </summary>
        public event EventHandler<RenderingDataEventArgs> RenderingData;

        /// <summary>
        /// Occurs when the currently selected audio device stops or loses its buffer.
        /// Call the <see cref="ChangeMedia"/> method and select a new audio device
        /// in order to output to a new audio device
        /// </summary>
        public event EventHandler AudioDeviceStopped;

        #endregion

        #region Event Raisers

        /// <summary>
        /// Raises the rendering video event.
        /// </summary>
        /// <param name="videoBlock">The block.</param>
        /// <param name="bitmap">The bitmap.</param>
        /// <param name="clock">The clock.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseRenderingVideoEvent(VideoBlock videoBlock, BitmapDataBuffer bitmap, TimeSpan clock)
        {
            if (RenderingVideo == null) return;

            var e = new RenderingVideoEventArgs(
                bitmap,
                videoBlock.ClosedCaptions,
                videoBlock.SmtpeTimeCode,
                videoBlock.DisplayPictureNumber,
                MediaCore.State,
                MediaCore.MediaInfo.Streams[videoBlock.StreamIndex],
                videoBlock.StartTime,
                videoBlock.Duration,
                clock);

            RenderingVideo?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the rendering audio event.
        /// </summary>
        /// <param name="buffer">The audio buffer.</param>
        /// <param name="bufferLength">Length of the buffer.</param>
        /// <param name="startTime">The start time.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="latency">The latency between the current buffer position and the real-time playback clock.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseRenderingAudioEvent(
            byte[] buffer, int bufferLength, TimeSpan startTime, TimeSpan duration, TimeSpan latency)
        {
            if (RenderingAudio == null) return;
            if (MediaCore == null || MediaCore.IsDisposed) return;
            if (MediaCore.MediaInfo.Streams.ContainsKey(MediaCore.State.AudioStreamIndex) == false) return;

            var e = new RenderingAudioEventArgs(
                    buffer,
                    bufferLength,
                    MediaCore.State,
                    MediaCore.MediaInfo.Streams[MediaCore.State.AudioStreamIndex],
                    startTime,
                    duration,
                    MediaCore.PlaybackPosition,
                    latency);

            RenderingAudio?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the rendering subtitles event.
        /// Returning true cancels the rendering of subtitles.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clock">The clock.</param>
        /// <returns>True if the rendering should be prevented.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool RaiseRenderingSubtitlesEvent(SubtitleBlock block, TimeSpan clock)
        {
            if (RenderingSubtitles == null) return false;

            var e = new RenderingSubtitlesEventArgs(
                    block.Text,
                    block.OriginalText,
                    block.OriginalTextType,
                    MediaCore.State,
                    MediaCore.MediaInfo.Streams[block.StreamIndex],
                    block.StartTime,
                    block.Duration,
                    clock);

            RenderingSubtitles?.Invoke(this, e);
            return e.Cancel;
        }

        /// <summary>
        /// Raises the rendering data event.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clock">The clock.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseRenderingDataEvent(DataBlock block, TimeSpan clock)
        {
            if (RenderingData == null) return;

            var e = new RenderingDataEventArgs(
                    MediaCore.State,
                    block,
                    MediaCore.MediaInfo.Streams[block.StreamIndex],
                    block.StartTime,
                    block.Duration,
                    clock);

            RenderingData?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the audio device stopped event.
        /// </summary>
        internal void RaiseAudioDeviceStoppedEvent() =>
            AudioDeviceStopped?.Invoke(this, EventArgs.Empty);

        #endregion
    }
}
