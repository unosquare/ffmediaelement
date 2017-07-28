namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using System;

    /// <summary>
    /// Represents a wrapper from an unmanaged FFmpeg audio frame
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Core.MediaFrame" />
    internal unsafe sealed class AudioFrame : MediaFrame, IDisposable
    {
        #region Private Members

        private AVFrame* m_Pointer = null;
        private bool IsDisposed = false;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioFrame" /> class.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="component">The component.</param>
        internal AudioFrame(AVFrame* frame, MediaComponent component)
            : base(frame, component)
        {
            m_Pointer = (AVFrame*)InternalPointer;

            // Compute the timespans
            //frame->pts = ffmpeg.av_frame_get_best_effort_timestamp(frame);
            StartTime = frame->pts == Utils.FFmpeg.AV_NOPTS ?
                TimeSpan.FromTicks(component.Container.MediaStartTimeOffset.Ticks) :
                TimeSpan.FromTicks(frame->pts.ToTimeSpan(StreamTimeBase).Ticks - component.Container.MediaStartTimeOffset.Ticks);

            // Compute the audio frame duration
            if (frame->pkt_duration != 0)
                Duration = frame->pkt_duration.ToTimeSpan(StreamTimeBase);
            else
                Duration = TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * 1000d * frame->nb_samples / frame->sample_rate, 0));

            EndTime = TimeSpan.FromTicks(StartTime.Ticks + Duration.Ticks);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the type of the media.
        /// </summary>
        public override MediaType MediaType => MediaType.Audio;

        /// <summary>
        /// Gets the pointer to the unmanaged frame.
        /// </summary>
        internal AVFrame* Pointer
        {
            get { return m_Pointer; }
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (m_Pointer != null)
                {
                    fixed (AVFrame** pointer = &m_Pointer)
                    {
                        RC.Current.Remove(*pointer);
                        ffmpeg.av_frame_free(pointer);
                    }
                }

                m_Pointer = null;
                InternalPointer = null;
                IsDisposed = true;
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="AudioFrame"/> class.
        /// </summary>
        ~AudioFrame()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
