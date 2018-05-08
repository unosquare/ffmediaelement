namespace Unosquare.FFME.Shared
{
    using ClosedCaptions;
    using Decoding;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// A pre-allocated, scaled video block. The buffer is in BGR, 24-bit format
    /// </summary>
    public sealed class VideoBlock : MediaBlock, IDisposable
    {
        #region Constructors and Descrutors

        /// <summary>
        /// Finalizes an instance of the <see cref="VideoBlock"/> class.
        /// </summary>
        ~VideoBlock()
        {
            Dispose(false);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the media type of the data
        /// </summary>
        public override MediaType MediaType => MediaType.Video;

        /// <summary>
        /// Gets a pointer to the first byte of the data buffer.
        /// The format is 32-bit BGRA
        /// </summary>
        public IntPtr Buffer => PictureBuffer;

        /// <summary>
        /// Gets the length of the buffer in bytes.
        /// </summary>
        public int BufferLength => PictureBufferLength;

        /// <summary>
        /// The picture buffer stride.
        /// Pixel Width * 32-bit color (4 byes) + alignment (typically 0 for modern hw).
        /// </summary>
        public int BufferStride => PictureBufferStride;

        /// <summary>
        /// Gets the number of horizontal pixels in the image.
        /// </summary>
        public int PixelWidth { get; private set; }

        /// <summary>
        /// Gets the number of vertical pixels in the image.
        /// </summary>
        public int PixelHeight { get; private set; }

        /// <summary>
        /// Gets the width of the aspect ratio.
        /// </summary>
        public int AspectWidth { get; internal set; }

        /// <summary>
        /// Gets the height of the aspect ratio.
        /// </summary>
        public int AspectHeight { get; internal set; }

        /// <summary>
        /// Gets the SMTPE time code.
        /// </summary>
        public string SmtpeTimecode { get; internal set; }

        /// <summary>
        /// Gets the display picture number (frame number).
        /// If not set by the decoder, this attempts to obtain it by dividing the start time by the
        /// frame duration
        /// </summary>
        public long DisplayPictureNumber { get; internal set; }

        /// <summary>
        /// Gets the coded picture number set by the decoder.
        /// </summary>
        public long CodedPictureNumber { get; internal set; }

        /// <summary>
        /// Gets the closed caption packets for this video block.
        /// </summary>
        public ReadOnlyCollection<ClosedCaptionPacket> ClosedCaptions { get; internal set; }

        /// <summary>
        /// The picture buffer length of the last allocated buffer
        /// </summary>
        internal int PictureBufferLength { get; private set; }

        /// <summary>
        /// Holds a reference to the last allocated buffer
        /// </summary>
        internal IntPtr PictureBuffer { get; private set; }

        /// <summary>
        /// Gets the picture buffer stride.
        /// </summary>
        internal int PictureBufferStride { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Allocates a block of memory suitable for a picture buffer
        /// and sets the corresponding properties.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="pixelFormat">The pixel format.</param>
        internal unsafe void EnsureAllocated(VideoFrame source, AVPixelFormat pixelFormat)
        {
            // Ensure proper allocation of the buffer
            // If there is a size mismatch between the wanted buffer length and the existing one,
            // then let's reallocate the buffer and set the new size (dispose of the existing one if any)
            var targetLength = ffmpeg.av_image_get_buffer_size(pixelFormat, source.Pointer->width, source.Pointer->height, 1);
            if (PictureBufferLength != targetLength)
            {
                Deallocate();
                PictureBuffer = new IntPtr(ffmpeg.av_malloc((uint)targetLength));
                PictureBufferLength = targetLength;
            }

            // Update related properties
            PictureBufferStride = ffmpeg.av_image_get_linesize(pixelFormat, source.Pointer->width, 0);
            PixelWidth = source.Pointer->width;
            PixelHeight = source.Pointer->height;
        }

        /// <summary>
        /// Deallocates the picture buffer and resets the related buffer properties
        /// </summary>
        private unsafe void Deallocate()
        {
            if (PictureBuffer == IntPtr.Zero) return;

            ffmpeg.av_free(PictureBuffer.ToPointer());
            PictureBuffer = IntPtr.Zero;
            PictureBufferLength = 0;
            PictureBufferStride = 0;
            PixelWidth = 0;
            PixelHeight = 0;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (!IsDisposed)
            {
                if (alsoManaged)
                {
                    // no code for managed dispose
                }

                Deallocate();
                IsDisposed = true;
            }
        }

        #endregion

    }
}
