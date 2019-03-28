namespace Unosquare.FFME
{
    using Engine;
    using Media;

    public static partial class Utilities
    {
        /// <summary>
        /// Creates a viedo seek index.
        /// </summary>
        /// <param name="mediaSource">The source URL.</param>
        /// <param name="streamIndex">Index of the stream. Use -1 for automatic stream selection.</param>
        /// <returns>
        /// The seek index object.
        /// </returns>
        public static VideoSeekIndex CreateVideoSeekIndex(string mediaSource, int streamIndex) =>
            MediaEngine.CreateVideoSeekIndex(mediaSource, streamIndex);

        /// <summary>
        /// Forces the pre-loading of the FFmpeg libraries according to the values of the
        /// <see cref="MediaElement.FFmpegDirectory"/> and <see cref="MediaElement.FFmpegLoadModeFlags"/>
        /// Also, sets the <see cref="MediaElement.FFmpegVersionInfo"/> property. Throws an exception
        /// if the libraries cannot be loaded.
        /// </summary>
        /// <returns>true if libraries were loaded, false if libraries were already loaded.</returns>
        public static bool LoadFFmpeg() => MediaEngine.LoadFFmpeg();

        /// <summary>
        /// Forces the unloading of FFmpeg libraries.
        /// </summary>
        public static void UnloadFFmpeg() => MediaEngine.UnloadFFmpeg();
    }
}
