namespace Unosquare.FFME.Shared
{
    /// <summary>
    /// Represents a set of options that are used to initialize a media container before opening the stream.
    /// </summary>
    public sealed class StreamOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StreamOptions"/> class.
        /// </summary>
        internal StreamOptions()
        {
            // placeholder
        }

        /// <summary>
        /// Contains options for the format context as documented:
        /// https://ffmpeg.org/ffmpeg-formats.html#Format-Options
        /// </summary>
        public StreamFormatOptions Format { get; } = new StreamFormatOptions();

        /// <summary>
        /// A dictionary containing generic input options for both:
        /// Global Codec Options: https://www.ffmpeg.org/ffmpeg-all.html#Codec-Options
        /// Demuxer-Private Options: https://ffmpeg.org/ffmpeg-all.html#Demuxers
        /// </summary>
        public StreamInputOptions Input { get; } = new StreamInputOptions();

        /// <summary>
        /// Gets the protocol prefix.
        /// Typically async for local files and empty for other types.
        /// </summary>
        public string ProtocolPrefix { get; set; } = null;
    }
}
