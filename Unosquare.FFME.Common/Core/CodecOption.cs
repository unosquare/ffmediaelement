namespace Unosquare.FFME.Core
{
    /// <summary>
    /// A single codec option along with a stream specifier.
    /// </summary>
    internal class CodecOption
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CodecOption"/> class.
        /// </summary>
        /// <param name="spec">The spec.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public CodecOption(StreamSpecifier spec, string key, string value)
        {
            StreamSpecifier = spec;
            Key = key;
            Value = value;
        }

        /// <summary>
        /// Gets or sets the stream specifier.
        /// </summary>
        public StreamSpecifier StreamSpecifier { get; set; }

        /// <summary>
        /// Gets or sets the option name
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the option value.
        /// </summary>
        public string Value { get; set; }
    }
}
