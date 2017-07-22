namespace Unosquare.FFME
{
    using System;
    using System.Windows.Media.Imaging;

    /// <summary>
    /// The Video Rendering event arguments
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class RenderingVideoEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingVideoEventArgs"/> class.
        /// </summary>
        /// <param name="bitmap">The bitmap.</param>
        /// <param name="position">The position.</param>
        public RenderingVideoEventArgs(WriteableBitmap bitmap, TimeSpan position)
            : base()
        {
            Bitmap = bitmap;
            Position = position;
        }


        /// <summary>
        /// Gets the clock position on which this rendering event was fired.
        /// </summary>
        public TimeSpan Position { get; }


        /// <summary>
        /// Gets the writable bitmap filled with the video frame pixels.
        /// </summary>
        public WriteableBitmap Bitmap { get; }

    }
}
