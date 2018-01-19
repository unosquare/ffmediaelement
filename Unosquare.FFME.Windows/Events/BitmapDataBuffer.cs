namespace Unosquare.FFME.Events
{
    using System;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    /// <summary>
    /// Contains metadata about a raw bitmap back-buffer
    /// </summary>
    public sealed class BitmapDataBuffer
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="BitmapDataBuffer" /> class.
        /// </summary>
        /// <param name="scan0">The scan0.</param>
        /// <param name="stride">The stride.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        /// <param name="pixelWidth">Width of the pixel.</param>
        /// <param name="pixelHeight">Height of the pixel.</param>
        /// <param name="dpiX">The dpi x.</param>
        /// <param name="dpiY">The dpi y.</param>
        /// <param name="palette">The palette.</param>
        /// <param name="pixelFormat">The pixel format.</param>
        private BitmapDataBuffer(
            IntPtr scan0,
            int stride,
            int bytesPerPixel,
            int pixelWidth,
            int pixelHeight,
            double dpiX,
            double dpiY,
            BitmapPalette palette,
            PixelFormat pixelFormat)
        {
            Scan0 = scan0;
            Stride = stride;
            BytesPerPixel = bytesPerPixel;
            BitsPerPixel = bytesPerPixel * 8;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            DpiX = dpiX;
            DpiY = dpiY;

            UpdateRect = new Int32Rect(0, 0, pixelWidth, pixelHeight);
            BufferLength = (uint)(Stride * PixelHeight);
            Palette = palette;
            PixelFormat = pixelFormat;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the length of the buffer (Stride x Pixel Height).
        /// </summary>
        public uint BufferLength { get; }

        /// <summary>
        /// Gets a pointer to the raw pixel data
        /// </summary>
        public IntPtr Scan0 { get; }

        /// <summary>
        /// Gets the byte width of each row of pixels
        /// </summary>
        public int Stride { get; }

        /// <summary>
        /// Gets the bits per pixel.
        /// </summary>
        public int BitsPerPixel { get; }

        /// <summary>
        /// Gets the bytes per pixel.
        /// </summary>
        public int BytesPerPixel { get; }

        /// <summary>
        /// Gets width of the bitmap
        /// </summary>
        public int PixelWidth { get; }

        /// <summary>
        /// Gets height of the bitmap
        /// </summary>
        public int PixelHeight { get; }

        /// <summary>
        /// Gets the DPI on the X axis
        /// </summary>
        public double DpiX { get; }

        /// <summary>
        /// Gets the DPI on the Y axis
        /// </summary>
        public double DpiY { get; }

        /// <summary>
        /// Gets the update rect.
        /// </summary>
        public Int32Rect UpdateRect { get; }

        /// <summary>
        /// Gets the palette.
        /// </summary>
        public BitmapPalette Palette { get; }

        /// <summary>
        /// Gets the pixel format.
        /// </summary>
        public PixelFormat PixelFormat { get; }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Creates a buffer reference from the bitmap. Please Lock the bitmap before calling this method.
        /// </summary>
        /// <param name="w">The w.</param>
        /// <returns>The corresponding buffer</returns>
        internal static BitmapDataBuffer FromWriteableBitmap(WriteableBitmap w)
        {
            var result = new BitmapDataBuffer(
                w.BackBuffer,
                w.BackBufferStride,
                w.Format.BitsPerPixel / 8,
                w.PixelWidth,
                w.PixelHeight,
                w.DpiX,
                w.DpiY,
                w.Palette,
                w.Format);

            return result;
        }

        #endregion
    }
}
