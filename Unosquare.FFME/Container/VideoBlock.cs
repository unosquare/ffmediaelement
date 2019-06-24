namespace Unosquare.FFME.Container
{
    using ClosedCaptions;
    using Common;
    using FFmpeg.AutoGen;
    using System.Collections.Generic;

    /// <inheritdoc />
    /// <summary>
    /// A pre-allocated, scaled video block. The buffer is in BGR, 24-bit format.
    /// </summary>
    internal sealed class VideoBlock : MediaBlock
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VideoBlock" /> class.
        /// </summary>
        internal VideoBlock()
            : base(MediaType.Video)
        {
            // placeholder
        }

        #region Properties

        /// <summary>
        /// Gets the number of horizontal pixels in the image.
        /// </summary>
        public int PixelWidth { get; private set; }

        /// <summary>
        /// Gets the number of vertical pixels in the image.
        /// </summary>
        public int PixelHeight { get; private set; }

        /// <summary>
        /// Gets the pixel aspect width.
        /// This is NOT the display aspect width.
        /// </summary>
        public int PixelAspectWidth { get; internal set; }

        /// <summary>
        /// Gets the pixel aspect height.
        /// This is NOT the display aspect height.
        /// </summary>
        public int PixelAspectHeight { get; internal set; }

        /// <summary>
        /// Gets the SMTPE time code.
        /// </summary>
        public string SmtpeTimeCode { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this frame was decoded in a hardware context.
        /// </summary>
        public bool IsHardwareFrame { get; internal set; }

        /// <summary>
        /// Gets the name of the hardware decoder if the frame was decoded in a hardware context.
        /// </summary>
        public string HardwareAcceleratorName { get; internal set; }

        /// <summary>
        /// Gets the display picture number (frame number).
        /// If not set by the decoder, this attempts to obtain it by dividing the start time by the
        /// frame duration.
        /// </summary>
        public long DisplayPictureNumber { get; internal set; }

        /// <summary>
        /// Gets the coded picture number set by the decoder.
        /// </summary>
        public long CodedPictureNumber { get; internal set; }

        /// <summary>
        /// Gets the closed caption packets for this video block.
        /// </summary>
        public IReadOnlyList<ClosedCaptionPacket> ClosedCaptions { get; internal set; }

        /// <summary>
        /// Gets the picture buffer stride.
        /// </summary>
        internal int PictureBufferStride { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Allocates a block of memory suitable for a picture buffer
        /// and sets the corresponding properties.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="pixelFormat">The pixel format.</param>
        /// <returns>True if the allocation was successful.</returns>
        internal unsafe bool Allocate(VideoFrame source, AVPixelFormat pixelFormat)
        {
            // Ensure proper allocation of the buffer
            // If there is a size mismatch between the wanted buffer length and the existing one,
            // then let's reallocate the buffer and set the new size (dispose of the existing one if any)
            var targetLength = ffmpeg.av_image_get_buffer_size(pixelFormat, source.Pointer->width, source.Pointer->height, 1);
            if (!Allocate(targetLength))
                return false;

            // Update related properties
            PictureBufferStride = ffmpeg.av_image_get_linesize(pixelFormat, source.Pointer->width, 0);
            PixelWidth = source.Pointer->width;
            PixelHeight = source.Pointer->height;

            return true;
        }

        /// <inheritdoc />
        protected override void Deallocate()
        {
            base.Deallocate();
            PictureBufferStride = 0;
            PixelWidth = 0;
            PixelHeight = 0;
        }

        #endregion
    }
}
