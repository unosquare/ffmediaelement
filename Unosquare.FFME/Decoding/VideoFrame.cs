namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using System;

    /// <summary>
    /// Represents a wrapper for an unmanaged ffmpeg video frame.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Core.MediaFrame" />
    internal unsafe sealed class VideoFrame : MediaFrame
    {
        #region Private Members

        private AVFrame* m_Pointer = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoFrame" /> class.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="component">The component.</param>
        internal VideoFrame(AVFrame* frame, MediaComponent component)
            : base(frame, component)
        {
            m_Pointer = (AVFrame*)InternalPointer;

            // for vide frames, we always get the best effort timestamp as dts and pts might
            // contain different times.
            frame->pts = ffmpeg.av_frame_get_best_effort_timestamp(frame);
            StartTime = frame->pts == Constants.AV_NOPTS ?
                TimeSpan.FromTicks(component.Container.MediaStartTimeOffset.Ticks) :
                TimeSpan.FromTicks(frame->pts.ToTimeSpan(StreamTimeBase).Ticks - component.Container.MediaStartTimeOffset.Ticks);

            var repeatFactor = 1d + (0.5d * frame->repeat_pict);
            var timeBase = ffmpeg.av_guess_frame_rate(component.Container.InputContext, component.Stream, frame);

            Duration = repeatFactor.ToTimeSpan(new AVRational { num = timeBase.den, den = timeBase.num });
            EndTime = TimeSpan.FromTicks(StartTime.Ticks + Duration.Ticks);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the type of the media.
        /// </summary>
        public override MediaType MediaType => MediaType.Video;

        /// <summary>
        /// Gets the pointer to the unmanaged frame.
        /// </summary>
        internal AVFrame* Pointer { get { return m_Pointer; } }

        #endregion

        #region Methods

        /// <summary>
        /// Releases internal frame
        /// </summary>
        protected override void Release()
        {
            if (m_Pointer == null) return;
            fixed (AVFrame** pointer = &m_Pointer)
                ffmpeg.av_frame_free(pointer);

            m_Pointer = null;
            InternalPointer = null;
        }

        #endregion

    }

}
