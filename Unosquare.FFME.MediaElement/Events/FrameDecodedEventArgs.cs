namespace Unosquare.FFME.Events
{
    using FFmpeg.AutoGen;

    /// <summary>
    /// Event arguments corresponding to the audio or video frame decoded events. Useful for capturing streams.
    /// </summary>
    public sealed unsafe class FrameDecodedEventArgs : InputFormatEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FrameDecodedEventArgs"/> class.
        /// </summary>
        /// <param name="frame">The audio or video frame pointer</param>
        /// <param name="context">The input format context</param>
        internal FrameDecodedEventArgs(AVFrame* frame, AVFormatContext* context)
            : base(context)
        {
            Frame = frame;
        }

        /// <summary>
        /// Gets the pointer to the audio or video frame that was decoded.
        /// </summary>
        public AVFrame* Frame { get; }
    }
}
