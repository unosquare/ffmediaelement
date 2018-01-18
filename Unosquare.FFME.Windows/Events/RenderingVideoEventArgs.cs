namespace Unosquare.FFME.Events
{
    using ClosedCaptions;
    using Shared;
    using System;

    /// <summary>
    /// The video rendering event arguments
    /// </summary>
    /// <seealso cref="EventArgs" />
    public sealed class RenderingVideoEventArgs : RenderingEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingVideoEventArgs" /> class.
        /// </summary>
        /// <param name="bitmap">The bitmap.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="closedCaptions">The closed captions.</param>
        /// <param name="smtpeTimecode">The smtpe timecode.</param>
        /// <param name="pictureNumber">The picture number.</param>
        /// <param name="startTime">The start time.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="clock">The clock.</param>
        internal RenderingVideoEventArgs(BitmapDataBuffer bitmap, StreamInfo stream, ClosedCaptionCollection closedCaptions, string smtpeTimecode, int pictureNumber, TimeSpan startTime, TimeSpan duration, TimeSpan clock)
            : base(stream, startTime, duration, clock)
        {
            PictureNumber = pictureNumber;
            Bitmap = bitmap;
            SmtpeTimecode = smtpeTimecode;
            ClosedCaptions = closedCaptions;
        }

        /// <summary>
        /// Gets the writable bitmap filled with the video frame pixels.
        /// Feel free to capture or change this buffer.
        /// </summary>
        public BitmapDataBuffer Bitmap { get; }

        /// <summary>
        /// Gets the closed caption decoded packets.
        /// </summary>
        public ClosedCaptionCollection ClosedCaptions { get; }

        /// <summary>
        /// Gets the display picture number (frame number).
        /// If not set by the decoder, this attempts to obtain it by dividing the start time by the
        /// frame duration
        /// </summary>
        public int PictureNumber { get; }

        /// <summary>
        /// Gets the SMTPE time code.
        /// </summary>
        public string SmtpeTimecode { get; }
    }
}
