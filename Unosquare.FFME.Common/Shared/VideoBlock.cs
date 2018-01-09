namespace Unosquare.FFME.Shared
{
    using ClosedCaptions;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A pre-allocated, scaled video block. The buffer is in BGR, 24-bit format
    /// </summary>
    public sealed class VideoBlock : MediaBlock, IDisposable
    {
        #region Private Members

        private bool IsDisposed = false; // To detect redundant calls

        #endregion

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
        /// The format is 24bit BGR
        /// </summary>
        public IntPtr Buffer => PictureBuffer;

        /// <summary>
        /// Gets the length of the buffer in bytes.
        /// </summary>
        public int BufferLength => PictureBufferLength;

        /// <summary>
        /// The picture buffer stride. 
        /// Pixel Width * 24-bit color (3 byes) + alignment (typically 0 for modern hw).
        /// </summary>
        public int BufferStride { get; internal set; }

        /// <summary>
        /// Gets the number of horizontal pixels in the image.
        /// </summary>
        public int PixelWidth { get; internal set; }

        /// <summary>
        /// Gets the number of vertical pixels in the image.
        /// </summary>
        public int PixelHeight { get; internal set; }

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
        public int DisplayPictureNumber { get; internal set; }

        /// <summary>
        /// Gets the coded picture number set by the decoder.
        /// </summary>
        public int CodedPictureNumber { get; internal set; }

        /// <summary>
        /// Gets the closed caption packets for this video block.
        /// </summary>
        public List<ClosedCaptionPacket> ClosedCaptions { get; internal set; }

        /// <summary>
        /// The picture buffer length of the last allocated buffer
        /// </summary>
        internal int PictureBufferLength { get; set; }

        /// <summary>
        /// Holds a reference to the last allocated buffer
        /// </summary>
        internal IntPtr PictureBuffer { get; set; }

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

                if (PictureBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(PictureBuffer);
                    PictureBuffer = IntPtr.Zero;
                    PictureBufferLength = 0;
                }

                IsDisposed = true;
            }
        }

        #endregion

    }
}
