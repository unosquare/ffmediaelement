namespace Unosquare.FFME.Decoding
{
    using ClosedCaptions;
    using Core;
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a wrapper for an unmanaged ffmpeg video frame.
    /// </summary>
    /// <seealso cref="MediaFrame" />
    internal sealed unsafe class VideoFrame : MediaFrame
    {
        #region Private Members

        private AVFrame* m_Pointer = null;
        private bool IsDisposed = false;

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

            var repeatFactor = 1d + (0.5d * frame->repeat_pict);
            var timeBase = ffmpeg.av_guess_frame_rate(component.Container.InputContext, component.Stream, frame);
            Duration = repeatFactor.ToTimeSpan(new AVRational { num = timeBase.den, den = timeBase.num });

            // for video frames, we always get the best effort timestamp as dts and pts might
            // contain different times.
            frame->pts = ffmpeg.av_frame_get_best_effort_timestamp(frame);

            HasValidStartTime = frame->pts != ffmpeg.AV_NOPTS_VALUE;
            StartTime = frame->pts == ffmpeg.AV_NOPTS_VALUE ?
                TimeSpan.FromTicks(0) :
                TimeSpan.FromTicks(frame->pts.ToTimeSpan(StreamTimeBase).Ticks - component.Container.MediaStartTimeOffset.Ticks);

            EndTime = TimeSpan.FromTicks(StartTime.Ticks + Duration.Ticks);

            // Picture Number and SMTPE TimeCode
            DisplayPictureNumber = frame->display_picture_number == 0 ?
                Extensions.ComputePictureNumber(StartTime, Duration, 1) :
                frame->display_picture_number;

            CodedPictureNumber = frame->coded_picture_number;
            SmtpeTimecode = Extensions.ComputeSmtpeTimeCode(component.StartTimeOffset, Duration, timeBase, DisplayPictureNumber);

            // Process side data such as CC packets
            for (var i = 0; i < frame->nb_side_data; i++)
            {
                var sideData = frame->side_data[i];

                // Get the Closed-Caption packets
                if (sideData->type == AVFrameSideDataType.AV_FRAME_DATA_A53_CC)
                {
                    // Parse 3 bytes at a time
                    for (var p = 0; p < sideData->size; p += 3)
                    {
                        var packet = new ClosedCaptionPacket(StartTime, sideData->data[p + 0], sideData->data[p + 1], sideData->data[p + 2]);
                        if (packet.PacketType == CCPacketType.NullPad || packet.PacketType == CCPacketType.Unrecognized)
                            continue;

                        // at this point, we have valid CC data
                        ClosedCaptions.Add(packet);
                    }

                    continue;
                }
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="VideoFrame"/> class.
        /// </summary>
        ~VideoFrame()
        {
            Dispose(false);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the type of the media.
        /// </summary>
        public override MediaType MediaType => MediaType.Video;

        /// <summary>
        /// Gets the closed caption data collected from the frame in CEA-708/EAS-608 format.
        /// </summary>
        public List<ClosedCaptionPacket> ClosedCaptions { get; } = new List<ClosedCaptionPacket>();

        /// <summary>
        /// Gets the display picture number (frame number).
        /// If not set by the decoder, this attempts to obtain it by dividing the start time by the
        /// frame duration
        /// </summary>
        public long DisplayPictureNumber { get; }

        /// <summary>
        /// Gets the coded picture number set by the decoder.
        /// </summary>
        public long CodedPictureNumber { get; }

        /// <summary>
        /// Gets the SMTPE time code.
        /// </summary>
        public string SmtpeTimecode { get; }

        /// <summary>
        /// Gets the pointer to the unmanaged frame.
        /// </summary>
        internal AVFrame* Pointer => m_Pointer;

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
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

        #endregion

    }
}
