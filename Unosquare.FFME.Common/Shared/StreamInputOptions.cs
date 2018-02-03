namespace Unosquare.FFME.Shared
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A dictionary containing generic input options for both:
    /// Global Codec Options: https://www.ffmpeg.org/ffmpeg-all.html#Codec-Options
    /// Demuxer-Private options: https://ffmpeg.org/ffmpeg-all.html#Demuxers
    /// </summary>
    public sealed class StreamInputOptions : Dictionary<string, string>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StreamInputOptions"/> class.
        /// </summary>
        internal StreamInputOptions()
            : base(512, StringComparer.InvariantCultureIgnoreCase)
        {
            // placeholder
        }

        /// <summary>
        /// Gets or sets the forced input format. If let null or empty,
        /// the input format will be selected automatically.
        /// </summary>
        public string ForcedInputFormat { get; set; }

        /// <summary>
        /// Gets or sets the amount of time to wait for a an open or read operation to complete.
        /// </summary>
        public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// A collection of well-known demuxer-specific, non-global format options
        /// TODO: (Floyd) Implement some of the more common names maybe?
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
