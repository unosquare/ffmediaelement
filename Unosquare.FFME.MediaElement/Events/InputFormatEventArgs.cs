namespace Unosquare.FFME.Events
{
    using FFmpeg.AutoGen;
    using System;

    /// <summary>
    /// Generic Input format event arguments. Useful for capturing streams.
    /// </summary>
    public abstract unsafe class InputFormatEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InputFormatEventArgs"/> class.
        /// </summary>
        /// <param name="context">The input format context.</param>
        protected InputFormatEventArgs(AVFormatContext* context)
        {
            InputContext = context;
        }

        /// <summary>
        /// Gets a pointer to the unmanaged input format context.
        /// </summary>
        public AVFormatContext* InputContext { get; }
    }
}
