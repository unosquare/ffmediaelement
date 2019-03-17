namespace Unosquare.FFME.Engine
{
    using Core;

    /// <summary>
    /// The load mode of FFmpeg Libraries
    /// </summary>
    public static class FFmpegLoadMode
    {
        /// <summary>
        /// The full features. Tries to load everything
        /// </summary>
        public static int FullFeatures { get; } =
            FFLibrary.LibAVCodec.FlagId |
            FFLibrary.LibAVDevice.FlagId |
            FFLibrary.LibAVFilter.FlagId |
            FFLibrary.LibAVFormat.FlagId |
            FFLibrary.LibAVUtil.FlagId |
            FFLibrary.LibSWResample.FlagId |
            FFLibrary.LibSWScale.FlagId;

        /// <summary>
        /// Loads everything except for AVDevice and AVFilter
        /// </summary>
        public static int MinimumFeatures { get; } =
            FFLibrary.LibAVCodec.FlagId |
            FFLibrary.LibAVFormat.FlagId |
            FFLibrary.LibAVUtil.FlagId |
            FFLibrary.LibSWResample.FlagId |
            FFLibrary.LibSWScale.FlagId;

        /// <summary>
        /// Loads the minimum set for Audio-only programs
        /// </summary>
        public static int AudioOnly { get; } =
            FFLibrary.LibAVCodec.FlagId |
            FFLibrary.LibAVFormat.FlagId |
            FFLibrary.LibAVUtil.FlagId |
            FFLibrary.LibSWResample.FlagId;

        /// <summary>
        /// Loads the minimum set for Video-only programs
        /// </summary>
        public static int VideoOnly { get; } =
            FFLibrary.LibAVCodec.FlagId |
            FFLibrary.LibAVFormat.FlagId |
            FFLibrary.LibAVUtil.FlagId |
            FFLibrary.LibSWScale.FlagId;
    }
}
