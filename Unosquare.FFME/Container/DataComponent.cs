namespace Unosquare.FFME.Container
{
    using FFmpeg.AutoGen;
    using System;
    using Unosquare.FFME.Diagnostics;

    /// <summary>
    /// Performs data stream extraction and decoding.
    /// </summary>
    /// <seealso cref="MediaComponent" />
    internal sealed unsafe class DataComponent : MediaComponent, ILoggingSource
    {
        #region Private Declarations

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DataComponent"/> class.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="streamIndex">Index of the stream.</param>
        internal DataComponent(MediaContainer container, int streamIndex)
            : base(container, streamIndex)
        {
        }

        #endregion

        #region Methods

        /// <summary>
        /// Converts decoded, raw frame data in the frame source into a a usable frame. <br />
        /// The process includes performing picture, samples or text conversions
        /// so that the decoded source frame data is easily usable in multimedia applications.
        /// </summary>
        /// <param name="input">The source frame to use as an input.</param>
        /// <param name="output">The target frame that will be updated with the source frame. If null is passed the frame will be instantiated.</param>
        /// <param name="previousBlock">The previousBlock blocks that may help guess some additional parameters for the input frame.</param>
        /// <returns>
        /// Return the updated output frame.
        /// </returns>
        /// <exception cref="ArgumentNullException">input cannot be null.</exception>
        public override bool MaterializeFrame(MediaFrame input, ref MediaBlock output, MediaBlock previousBlock)
        {
            if (output == null) output = new DataBlock();
            if (input is DataFrame == false || output is DataBlock == false)
                throw new ArgumentNullException($"{nameof(input)} and {nameof(output)} are either null or not of a compatible media type '{Common.MediaType.Data}'");

            var source = (DataFrame)input;
            var target = (DataBlock)output;

            // Set the target main data
            if (source.HasValidStartTime)
            {
                target.StartTime = source.StartTime;
                target.Duration = source.Duration;
                target.EndTime = source.EndTime;
            }
            else
            {
                // Fix data stream without PTS : sync with the main stream (=Video)
                target.StartTime = Container.Components.Main.LastFramePts.Value.ToTimeSpan(Container.Components.Main.Stream->time_base);
                target.Duration = source.Duration;
                target.EndTime = target.StartTime + source.Duration;
            }

            // Set the target other data
            target.CompressedSize = source.CompressedSize;
            target.Bytes = source.Bytes;
            target.StreamIndex = source.StreamIndex;
            target.IsStartTimeGuessed = source.HasValidStartTime == false;

            return true;
        }

        /// <summary>
        /// Creates a frame source object given the raw FFmpeg frame reference.
        /// </summary>
        /// <param name="packet">The raw FFmpeg frame pointer.</param>
        /// <returns>The media frame.</returns>
        protected override unsafe MediaFrame CreateFrameSource(IntPtr packet)
        {
            return new DataFrame((AVPacket*)packet, this);
        }

        #endregion
    }
}