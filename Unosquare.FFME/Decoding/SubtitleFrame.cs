namespace Unosquare.FFME.Decoding
{
    using Engine;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;

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
        private bool IsDisposed;

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
            // Extract timing information (pts for Subtitles is always in AV_TIME_BASE units)
            HasValidStartTime = frame->pts != ffmpeg.AV_NOPTS_VALUE;
            var timeOffset = frame->pts.ToTimeSpan(ffmpeg.AV_TIME_BASE);

            // start_display_time and end_display_time are relative to timeOffset
            StartTime = TimeSpan.FromMilliseconds(timeOffset.TotalMilliseconds + frame->start_display_time);
            EndTime = TimeSpan.FromMilliseconds(timeOffset.TotalMilliseconds + frame->end_display_time);
            Duration = TimeSpan.FromMilliseconds(frame->end_display_time - frame->start_display_time);

            // Extract text strings
            TextType = AVSubtitleType.SUBTITLE_NONE;

            for (var i = 0; i < frame->num_rects; i++)
            {
                var rect = frame->rects[i];

                if (rect->type == AVSubtitleType.SUBTITLE_TEXT)
                {
                    if (rect->text == null) continue;
                    Text.Add(Utilities.PtrToStringUTF8(rect->text));
                    TextType = AVSubtitleType.SUBTITLE_TEXT;
                    break;
                }

                if (rect->type == AVSubtitleType.SUBTITLE_ASS)
                {
                    if (rect->ass == null) continue;
                    Text.Add(Utilities.PtrToStringUTF8(rect->ass));
                    TextType = AVSubtitleType.SUBTITLE_ASS;
                    break;
                }

                TextType = rect->type;
            }
        }

        #endregion

        #region Properties

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
        public AVSubtitleType TextType { get; }

        /// <summary>
        /// Gets the pointer to the unmanaged subtitle struct
        /// </summary>
        internal AVSubtitle* Pointer => (AVSubtitle*)InternalPointer;

        #endregion

        #region Static Methods

        /// <inheritdoc />
        public override void Dispose()
        {
            lock (DisposeLock)
            {
                if (IsDisposed)
                    return;

                if (InternalPointer != IntPtr.Zero)
                    ReleaseAVSubtitle(Pointer);

                InternalPointer = IntPtr.Zero;
                IsDisposed = true;
            }
        }

        #endregion

    }
}
