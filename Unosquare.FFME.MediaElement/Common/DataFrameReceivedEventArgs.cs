namespace Unosquare.FFME.Common
{
    using System;

    /// <summary>
    /// Event arguments corresponding to the reading of data (non-media) frames.
    /// </summary>
    public sealed class DataFrameReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataFrameReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="dataFrame">The data frame.</param>
        /// <param name="stream">The stream.</param>
        internal DataFrameReceivedEventArgs(DataFrame dataFrame, StreamInfo stream)
        {
            Frame = dataFrame;
            Stream = stream;
        }

        /// <summary>
        /// Contains the data frame.
        /// </summary>
        public DataFrame Frame { get; }

        /// <summary>
        /// Gets the associated stream information.
        /// </summary>
        public StreamInfo Stream { get; }
    }
}
