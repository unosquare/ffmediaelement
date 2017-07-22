namespace Unosquare.FFME
{
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Windows.Media.Imaging;

    partial class MediaElement
    {
        /// <summary>
        /// Occurs right before the video is presented on the screen.
        /// You can update the image before it becomes rendered.
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

        /// <summary>
        /// Raises the rendering video event.
        /// </summary>
        /// <param name="bitmap">The bitmap.</param>
        /// <param name="position">The position.</param>
        /// <param name="duration">The duration.</param>
        internal void RaiseRenderingVideoEvent(WriteableBitmap bitmap, TimeSpan position, TimeSpan duration)
        {
            RenderingVideo?.Invoke(this, new RenderingVideoEventArgs(bitmap, position, duration));
        }


        /// <summary>
        /// Raises the rendering audio event.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="length">The length.</param>
        /// <param name="position">The position.</param>
        /// <param name="duration">The duration.</param>
        internal void RaiseRenderingAudioEvent(IntPtr buffer, int length, TimeSpan position, TimeSpan duration)
        {
            RenderingAudio?.Invoke(this, new RenderingAudioEventArgs(buffer, length, position, duration));
        }


        /// <summary>
        /// Raises the rendering subtitles event.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="originalText">The original text.</param>
        /// <param name="format">The format.</param>
        /// <param name="position">The position.</param>
        /// <param name="duration">The duration.</param>
        internal void RaiseRenderingSubtitlesEvent(List<string> text, List<string> originalText, AVSubtitleType format, TimeSpan position, TimeSpan duration)
        {
            RenderingSubtitles?.Invoke(this, new RenderingSubtitlesEventArgs(text, originalText, format, position, duration));
        }

    }
}
