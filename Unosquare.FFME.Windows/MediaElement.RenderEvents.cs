namespace Unosquare.FFME
{
    using Shared;
    using System;
    using Events;
    using System.Runtime.CompilerServices;
    using System.Windows.Media.Imaging;

    public partial class MediaElement
    {
        #region Events

        /// <summary>
        /// Occurs right before the video is presented on the screen.
        /// You can update the pizels on the bitmap before it is rendered on the screen.
        /// Or you could take a screenshot.
        /// Ensure you handle this very quickly as it runs on the UI thread.
        /// </summary>
        public event EventHandler<RenderingVideoEventArgs> RenderingVideo;

        /// <summary>
        /// Occurs right before the audio is added to the audio buffer.
        /// You can update the bytes before they are enqueued.
        /// Ensure you handle this quickly before you get choppy audio.
        /// </summary>
        public event EventHandler<RenderingAudioEventArgs> RenderingAudio;

        /// <summary>
        /// Occurs right before the subtitles are rendered.
        /// You can update the text.
        /// Ensure you handle this quickly before you get choppy subtitles.
        /// </summary>
        public event EventHandler<RenderingSubtitlesEventArgs> RenderingSubtitles;

        #endregion

        #region Event Raisers

        /// <summary>
        /// Raises the rendering video event.
        /// </summary>
        /// <param name="bitmap">The bitmap.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="smtpeTimecode">The smtpe timecode.</param>
        /// <param name="pictureNumber">The picture number.</param>
        /// <param name="startTime">The start time.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="clock">The clock.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseRenderingVideoEvent(
            WriteableBitmap bitmap, StreamInfo stream, string smtpeTimecode, int pictureNumber, TimeSpan startTime, TimeSpan duration, TimeSpan clock)
        {
            RenderingVideo?.Invoke(this, new RenderingVideoEventArgs(bitmap, stream, smtpeTimecode, pictureNumber, startTime, duration, clock));
        }

        /// <summary>
        /// Raises the rendering audio event.
        /// </summary>
        /// <param name="audioBlock">The audio block.</param>
        /// <param name="clock">The clock.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseRenderingAudioEvent(AudioBlock audioBlock, TimeSpan clock)
        {
            var e = new RenderingAudioEventArgs(
                    audioBlock.Buffer,
                    audioBlock.BufferLength,
                    MediaCore.MediaInfo.Streams[audioBlock.StreamIndex],
                    audioBlock.StartTime,
                    audioBlock.Duration,
                    clock);

            RenderingAudio?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the rendering subtitles event.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clock">The clock.</param>
        /// <returns>True if the rendering should be prevented</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool RaiseRenderingSubtitlesEvent(SubtitleBlock block, TimeSpan clock)
        {
            var e = new RenderingSubtitlesEventArgs(
                    block.Text,
                    block.OriginalText,
                    block.OriginalTextType,
                    MediaCore.MediaInfo.Streams[block.StreamIndex],
                    block.StartTime,
                    block.Duration,
                    clock);

            RenderingSubtitles?.Invoke(this, e);
            return e.Cancel;
        }

        #endregion

    }
}
