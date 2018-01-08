namespace Unosquare.FFME.Shared
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A dictionary containing generic input options for both:
    /// Global Codec Options: https://www.ffmpeg.org/ffmpeg-all.html#Codec-Options
    /// Demuxer-Private options: https://ffmpeg.org/ffmpeg-all.html#Demuxers
    /// </summary>
    public class MediaInputOptions : Dictionary<string, string>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaInputOptions"/> class.
        /// </summary>
        public MediaInputOptions()
            : base(512, StringComparer.InvariantCultureIgnoreCase)
        {
            // placeholder
        }

        /// <summary>
        /// A collection of well-known demuxer-specific, non-global format options
        /// </summary>
        public static class Names
        {
            /// <summary>
            /// mpegts
            /// </summary>
            public const string ScanAllPmts = "scan_all_pmts";
        }
    }
}
