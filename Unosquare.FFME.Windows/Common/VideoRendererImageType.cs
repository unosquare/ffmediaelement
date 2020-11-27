namespace Unosquare.FFME.Common
{
    /// <summary>
    /// Enumerates the different Video renderer image types.
    /// </summary>
    public enum VideoRendererImageType
    {
        /// <summary>
        /// Uses a tear-free WriteableBitmap.
        /// </summary>
        WriteableBitmap,

        /// <summary>
        /// Uses the faster, non tear-free InteropBitmap.
        /// </summary>
        InteropBitmap
    }
}
