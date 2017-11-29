﻿namespace Unosquare.FFME.Decoding
{
    using ClosedCaptions;
    using Core;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents a wrapper for an unmanaged ffmpeg video frame.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Decoding.MediaFrame" />
    internal unsafe sealed class VideoFrame : MediaFrame
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
            const int AV_TIMECODE_STR_SIZE = 16 + 1;

            m_Pointer = (AVFrame*)InternalPointer;

            var repeatFactor = 1d + (0.5d * frame->repeat_pict);
            var timeBase = ffmpeg.av_guess_frame_rate(component.Container.InputContext, component.Stream, frame);
            Duration = repeatFactor.ToTimeSpan(new AVRational { num = timeBase.den, den = timeBase.num });

            // for video frames, we always get the best effort timestamp as dts and pts might
            // contain different times.
            frame->pts = ffmpeg.av_frame_get_best_effort_timestamp(frame);

            HasValidStartTime = frame->pts != FFmpegEx.AV_NOPTS;
            StartTime = frame->pts == FFmpegEx.AV_NOPTS ?
                TimeSpan.FromTicks(component.Container.MediaStartTimeOffset.Ticks) :
                TimeSpan.FromTicks(frame->pts.ToTimeSpan(StreamTimeBase).Ticks - component.Container.MediaStartTimeOffset.Ticks);

            EndTime = TimeSpan.FromTicks(StartTime.Ticks + Duration.Ticks);

            DisplayPictureNumber = frame->display_picture_number == 0 ?
                (int)Math.Round((double)StartTime.Ticks / Duration.Ticks, 0) : 0;

            CodedPictureNumber = frame->coded_picture_number;

            // SMTPE timecode calculation
            var timeCodeInfo = (AVTimecode*)ffmpeg.av_malloc((ulong)Marshal.SizeOf(typeof(AVTimecode)));
            var startFrameNumber = (int)Math.Round((double)component.StartTimeOffset.Ticks / Duration.Ticks, 0);
            ffmpeg.av_timecode_init(timeCodeInfo, timeBase, 0, startFrameNumber, null);
            var isNtsc = timeBase.num == 30000 && timeBase.den == 1001;
            var frameNumber = isNtsc ? 
                ffmpeg.av_timecode_adjust_ntsc_framenum2(DisplayPictureNumber, (int)timeCodeInfo->fps) : 
                DisplayPictureNumber;

            var timeCode = ffmpeg.av_timecode_get_smpte_from_framenum(timeCodeInfo, DisplayPictureNumber);
            var timeCodeBuffer = (byte*)ffmpeg.av_malloc(AV_TIMECODE_STR_SIZE);

            ffmpeg.av_timecode_make_smpte_tc_string(timeCodeBuffer, timeCode, 1);
            SmtpeTimecode = Marshal.PtrToStringAnsi(new IntPtr(timeCodeBuffer));

            ffmpeg.av_free(timeCodeInfo);
            ffmpeg.av_free(timeCodeBuffer);

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
        public int DisplayPictureNumber { get; }

        /// <summary>
        /// Gets the coded picture number set by the decoder.
        /// </summary>
        public int CodedPictureNumber { get; }

        /// <summary>
        /// Gets the SMTPE time code.
        /// </summary>
        public string SmtpeTimecode { get; }

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
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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

        #endregion

    }
}
