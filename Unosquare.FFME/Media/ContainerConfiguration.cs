namespace Unosquare.FFME.Media
{
    using Engine;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Represents a set of options that are used to initialize a media container before opening the stream.
    /// This includes both, demuxer and decoder options.
    /// </summary>
    public sealed class ContainerConfiguration
    {
        /// <summary>
        /// The scan all PMTS private option name.
        /// </summary>
        internal const string ScanAllPmts = "scan_all_pmts";

        /// <summary>
        /// Initializes a new instance of the <see cref="ContainerConfiguration"/> class.
        /// </summary>
        internal ContainerConfiguration()
        {
            // placeholder
        }

        /// <summary>
        /// Gets or sets the forced input format. If let null or empty,
        /// the input format will be selected automatically.
        /// </summary>
        public string ForcedInputFormat { get; set; }

        /// <summary>
        /// Gets the protocol prefix.
        /// Typically async for local files and empty for other types.
        /// </summary>
        public string ProtocolPrefix { get; set; }

        /// <summary>
        /// Gets or sets the amount of time to wait for a an open or read
        /// operation to complete before it times out. It is 30 seconds by default.
        /// </summary>
        public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Contains global options for the demuxer. For additional info
        /// please see: https://ffmpeg.org/ffmpeg-formats.html#Format-Options.
        /// </summary>
        public DemuxerGlobalOptions GlobalOptions { get; } = new DemuxerGlobalOptions();

        /// <summary>
        /// Contains private demuxer options. For additional info
        /// please see: https://ffmpeg.org/ffmpeg-all.html#Demuxers.
        /// </summary>
        public Dictionary<string, string> PrivateOptions { get; } =
            new Dictionary<string, string>(512, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Gets a list of global options for the demuxers. These options apply to all demuxers.
        /// </summary>
        public ReadOnlyCollection<OptionMetadata> GlobalOptionsMeta { get; } = MediaEngine.InputFormatOptionsGlobal;

        /// <summary>
        /// Retrieves a dictionary of private options available to each of the registered demuxers.
        /// </summary>
        public ReadOnlyDictionary<string, ReadOnlyCollection<OptionMetadata>> PrivateOptionsMeta { get; } = MediaEngine.InputFormatOptions;
    }
}
