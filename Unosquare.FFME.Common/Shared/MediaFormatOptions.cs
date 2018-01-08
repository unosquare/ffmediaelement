namespace Unosquare.FFME.Shared
{
    using System;

    /// <summary>
    /// Contains options for the format context as documented:
    /// https://ffmpeg.org/ffmpeg-formats.html#Format-Options
    /// TODO: There are still quite a bit of options that have not been implemented.
    /// </summary>
    public class MediaFormatOptions
    {
        /// <summary>
        /// Port of avioflags direct
        /// </summary>
        public bool EnableReducedBuffering { get; set; }

        /// <summary>
        /// Set probing size in bytes, i.e. the size of the data to analyze to get stream information. 
        /// A higher value will enable detecting more information in case it is dispersed into the stream,
        /// but will increase latency. Must be an integer not lesser than 32. It is 5000000 by default.
        /// </summary>
        public int ProbeSize { get; set; }

        /// <summary>
        /// Set packet size.
        /// </summary>
        public int PacketSize { get; set; }

        /// <summary>
        /// Ignore index.
        /// Port of ffflags
        /// </summary>
        public bool FlagIgnoreIndex { get; set; }

        /// <summary>
        /// Enable fast, but inaccurate seeks for some formats.
        /// Port of ffflags
        /// </summary>
        public bool FlagEnableFastSeek { get; set; }

        /// <summary>
        /// Generate PTS.
        /// Port of genpts
        /// </summary>
        public bool FlagGeneratePts { get; set; }

        /// <summary>
        /// Do not fill in missing values that can be exactly calculated.
        /// Port of ffflags
        /// </summary>
        public bool FlagEnableNoFillin { get; set; }

        /// <summary>
        /// Ignore DTS.
        /// Port of ffflags
        /// </summary>
        public bool FlagIgnoreDts { get; set; }

        /// <summary>
        /// Discard corrupted frames.
        /// Port of ffflags
        /// </summary>
        public bool FlagDiscardCorrupt { get; set; }

        /// <summary>
        /// Try to interleave output packets by DTS.
        /// Port of ffflags
        /// </summary>
        public bool FlagSortDts { get; set; }

        /// <summary>
        /// Do not merge side data.
        /// Port of ffflags
        /// </summary>
        public bool FlagKeepSideData { get; set; }

        /// <summary>
        /// Enable RTP MP4A-LATM payload.
        /// Port of ffflags
        /// </summary>
        public bool FlagEnableLatmPayload { get; set; }

        /// <summary>
        /// Reduce the latency introduced by optional buffering
        /// Port of ffflags
        /// </summary>
        public bool FlagNoBuffer { get; set; }

        /// <summary>
        /// Stop muxing at the end of the shortest stream. 
        /// It may be needed to increase max_interleave_delta to avoid flushing the longer streams before EOF.
        /// Port of ffflags
        /// </summary>
        public bool FlagStopAtShortest { get; set; }

        /// <summary>
        /// Allow seeking to non-keyframes on demuxer level when supported if set to 1. Default is 0.
        /// </summary>
        public bool SeekToAny { get; set; }

        /// <summary>
        /// Gets or sets the maximum duration to be analyzed before ifentifying stream information.
        /// In realtime streams this can be reduced to reduce latency (i.e. TimeSpan.Zero)
        /// </summary>
        public TimeSpan MaxAnalyzeDuration { get; set; }

        /// <summary>
        /// Set decryption key.
        /// </summary>
        public string CryptoKey { get; set; }
    }
}
