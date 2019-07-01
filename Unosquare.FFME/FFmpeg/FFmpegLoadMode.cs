namespace FFmpeg.AutoGen
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The load mode of FFmpeg Libraries.
    /// </summary>
    public static class FFmpegLoadMode
    {
        /// <summary>
        /// Gets the individual library flag identifiers.
        /// </summary>
        public static IReadOnlyDictionary<string, int> LibraryFlags { get; } = FFLibrary.All.ToDictionary(k => k.Name, v => v.FlagId);

        /// <summary>
        /// The full features. Tries to load everything.
        /// </summary>
        public static int FullFeatures { get; } =
            FFLibrary.LibAVCodec.FlagId |
            FFLibrary.LibAVDevice.FlagId |
            FFLibrary.LibPostProc.FlagId |
            FFLibrary.LibAVFilter.FlagId |
            FFLibrary.LibAVFormat.FlagId |
            FFLibrary.LibAVUtil.FlagId |
            FFLibrary.LibSWResample.FlagId |
            FFLibrary.LibSWScale.FlagId;

        /// <summary>
        /// Loads everything except for AVDevice and AVFilter.
        /// </summary>
        public static int MinimumFeatures { get; } =
            FFLibrary.LibAVCodec.FlagId |
            FFLibrary.LibAVFormat.FlagId |
            FFLibrary.LibAVUtil.FlagId |
            FFLibrary.LibSWResample.FlagId |
            FFLibrary.LibSWScale.FlagId;

        /// <summary>
        /// Loads the minimum set for Audio-only programs.
        /// </summary>
        public static int AudioOnly { get; } =
            FFLibrary.LibAVCodec.FlagId |
            FFLibrary.LibAVFormat.FlagId |
            FFLibrary.LibAVUtil.FlagId |
            FFLibrary.LibSWResample.FlagId;

        /// <summary>
        /// Loads the minimum set for Video-only programs.
        /// </summary>
        public static int VideoOnly { get; } =
            FFLibrary.LibAVCodec.FlagId |
            FFLibrary.LibAVFormat.FlagId |
            FFLibrary.LibAVUtil.FlagId |
            FFLibrary.LibSWScale.FlagId;
    }
}
