namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents a wrapper for an unmanaged Subtitle frame.
    /// TODO: Only text (ASS and SRT) subtitles are supported currently.
    /// There is no support to bitmap subtitles.
    /// </summary>
    /// <seealso cref="MediaFrame" />
    internal sealed unsafe class SubtitleFrame : MediaFrame
    {
        #region Private Members

        private readonly object DisposeLock = new object();
        private AVSubtitle* m_Pointer = null;
        private bool IsDisposed = false;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SubtitleFrame" /> class.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="component">The component.</param>
        internal SubtitleFrame(AVSubtitle* frame, MediaComponent component)
            : base(frame, component)
        {
            m_Pointer = (AVSubtitle*)InternalPointer;

            // Extract timing information (pts for Subtitles is always in AV_TIME_BASE units)
            HasValidStartTime = frame->pts != ffmpeg.AV_NOPTS_VALUE;
            var timeOffset = TimeSpan.FromTicks(frame->pts.ToTimeSpan(ffmpeg.AV_TIME_BASE).Ticks - component.Container.MediaStartTimeOffset.Ticks);

            // start_display_time and end_display_time are relative to timeOffset
            StartTime = TimeSpan.FromTicks(timeOffset.Ticks + Convert.ToInt64(frame->start_display_time).ToTimeSpan(StreamTimeBase).Ticks);
            EndTime = TimeSpan.FromTicks(timeOffset.Ticks + Convert.ToInt64(frame->end_display_time).ToTimeSpan(StreamTimeBase).Ticks);
            Duration = TimeSpan.FromTicks(EndTime.Ticks - StartTime.Ticks);

            // Extract text strings
            TextType = AVSubtitleType.SUBTITLE_NONE;

            for (var i = 0; i < frame->num_rects; i++)
            {
                var rect = frame->rects[i];

                if (rect->type == AVSubtitleType.SUBTITLE_TEXT)
                {
                    if (rect->text != null)
                    {
                        Text.Add(FFInterop.PtrToStringUTF8(rect->text));
                        TextType = AVSubtitleType.SUBTITLE_TEXT;
                        break;
                    }
                }
                else if (rect->type == AVSubtitleType.SUBTITLE_ASS)
                {
                    if (rect->ass != null)
                    {
                        Text.Add(FFInterop.PtrToStringUTF8(rect->ass));
                        TextType = AVSubtitleType.SUBTITLE_ASS;
                        break;
                    }
                }
                else
                {
                    TextType = rect->type;
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the type of the media.
        /// </summary>
        public override MediaType MediaType => MediaType.Subtitle;

        /// <summary>
        /// Gets lines of text that the subtitle frame contains.
        /// </summary>
        public List<string> Text { get; } = new List<string>(16);

        /// <summary>
        /// Gets the type of the text.
        /// </summary>
        /// <value>
        /// The type of the text.
        /// </value>
        public AVSubtitleType TextType { get; } = AVSubtitleType.SUBTITLE_NONE;

        /// <summary>
        /// Gets the pointer to the unmanaged subtitle struct
        /// </summary>
        internal AVSubtitle* Pointer => m_Pointer;

        #endregion

        #region Static Methods

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public override void Dispose() =>
            Dispose(true);

        /// <summary>
        /// Allocates an AVSubtitle struct in unmanaged memory,
        /// </summary>
        /// <returns>The subtitle struct pointer</returns>
        internal static AVSubtitle* AllocateSubtitle()
        {
            return (AVSubtitle*)ffmpeg.av_malloc((ulong)Marshal.SizeOf(typeof(AVSubtitle)));
        }

        /// <summary>
        /// Deallocates the subtitle struct used to create in managed memory.
        /// </summary>
        /// <param name="frame">The frame.</param>
        internal static void DeallocateSubtitle(AVSubtitle* frame)
        {
            if (frame == null) return;
            ffmpeg.avsubtitle_free(frame);
            ffmpeg.av_free(frame);
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            lock (DisposeLock)
            {
                if (IsDisposed) return;

                if (m_Pointer != null)
                    DeallocateSubtitle(m_Pointer);

                m_Pointer = null;
                InternalPointer = null;
                IsDisposed = true;
            }
        }

        #endregion

    }
}
