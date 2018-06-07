namespace Unosquare.FFME.Shared
{
    /// <summary>
    /// A scaled, preallocated audio frame container.
    /// The buffer is in 16-bit signed, interleaved sample data
    /// </summary>
    public sealed class AudioBlock : MediaBlock
    {
        #region Properties

        /// <summary>
        /// Gets the sample rate.
        /// </summary>
        public int SampleRate { get; internal set; }

        /// <summary>
        /// Gets the channel count.
        /// </summary>
        public int ChannelCount { get; internal set; }

        /// <summary>
        /// Gets the available samples per channel.
        /// </summary>
        public int SamplesPerChannel { get; internal set; }

        /// <summary>
        /// Gets the length of the samples buffer. This might differ from the <see cref="MediaBlock.BufferLength"/>
        /// property after scaling but must always be less than or equal to it.
        /// </summary>
        /// <value>
        /// The length of the samples buffer.
        /// </value>
        public int SamplesBufferLength { get; internal set; }

        /// <summary>
        /// Gets the media type of the data
        /// </summary>
        public override MediaType MediaType => MediaType.Audio;

        #endregion

        #region Methods

        /// <summary>
        /// Deallocates the buffer and resets the related buffer properties
        /// </summary>
        protected override void Deallocate()
        {
            base.Deallocate();
            SampleRate = default;
            ChannelCount = default;
            SamplesPerChannel = default;
            SamplesBufferLength = 0;
        }

        #endregion
    }
}
