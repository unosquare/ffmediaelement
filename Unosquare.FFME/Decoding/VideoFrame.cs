namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents a wrapper for an unmanaged ffmpeg video frame.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Core.MediaFrame" />
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
            m_Pointer = (AVFrame*)InternalPointer;

            // for vide frames, we always get the best effort timestamp as dts and pts might
            // contain different times.
            frame->pts = ffmpeg.av_frame_get_best_effort_timestamp(frame);
            StartTime = frame->pts == Utils.FFmpeg.AV_NOPTS ?
                TimeSpan.FromTicks(component.Container.MediaStartTimeOffset.Ticks) :
                TimeSpan.FromTicks(frame->pts.ToTimeSpan(StreamTimeBase).Ticks - component.Container.MediaStartTimeOffset.Ticks);

            var repeatFactor = 1d + (0.5d * frame->repeat_pict);
            var timeBase = ffmpeg.av_guess_frame_rate(component.Container.InputContext, component.Stream, frame);

            Duration = repeatFactor.ToTimeSpan(new AVRational { num = timeBase.den, den = timeBase.num });
            EndTime = TimeSpan.FromTicks(StartTime.Ticks + Duration.Ticks);

            // TODO: Implement closed captions data parsing.

            for (var i = 0; i < frame->nb_side_data; i++)
            {
                var sideData = frame->side_data[i];
                if (sideData->type != AVFrameSideDataType.AV_FRAME_DATA_A53_CC) continue;

                // parse struct https://en.wikipedia.org/wiki/CEA-708
                // cc_data_pkt
                for (var p = 0; p < sideData->size; p += 3)
                {

                    // check first 5 bits are 1
                    if ((sideData->data[p] & 0xF8) != 0xF8) break;

                    // if we don't have a valid packet, discard it and move on to the next one
                    if ((sideData->data[p] & 0x04) == 0) continue;

                    // if we don't have a standard packet type (NTSC_CC_FIELD_1 = 0, NTSC_CC_FIELD_2 = 1)
                    // then just break because we can't parse other packet types (i.e. Packet type 3)
                    var ccField = (sideData->data[p] & 0x03);
                    if (ccField != 0 && ccField != 1) break;

                    // Ignore null padding packets (it is 128 and not 9 because the first bit of each byte is the parity 1 bit)
                    if (sideData->data[p + 1] == 128 || sideData->data[p + 2] == 128) continue;

                    // Create the EIA-608 Caption Command
                    var captionCommand = new Eia608Data(ccField, sideData->data[p + 1], sideData->data[p + 2]);

                    // at this point, the following 2 bytes are the CC data!
                    ClosedCaptions.Add(captionCommand);
                }
            }

            foreach (var cc in ClosedCaptions)
                component.Container.Logger.Log(MediaLogMessageType.Info, $"CC: {cc}");
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

        /// <summary>
        /// Gets the closed caption data collected from the frame in CEA-708 format.
        /// </summary>
        public List<Eia608Data> ClosedCaptions { get; } = new List<Eia608Data>();

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
                    fixed (AVFrame** pointer = &m_Pointer)
                    {
                        RC.Current.Remove(*pointer);
                        ffmpeg.av_frame_free(pointer);
                    }

                m_Pointer = null;
                InternalPointer = null;
                IsDisposed = true;
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="VideoFrame"/> class.
        /// </summary>
        ~VideoFrame()
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
