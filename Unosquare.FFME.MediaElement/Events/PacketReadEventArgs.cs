namespace Unosquare.FFME.Events
{
    using FFmpeg.AutoGen;

    /// <summary>
    /// Event arguments corresponding to the packet reading event. Useful for capturing streams.
    /// </summary>
    public sealed unsafe class PacketReadEventArgs : InputFormatEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PacketReadEventArgs"/> class.
        /// </summary>
        /// <param name="packet">The packet pointer.</param>
        /// <param name="context">The input format context.</param>
        internal PacketReadEventArgs(AVPacket* packet, AVFormatContext* context)
            : base(context)
        {
            Packet = packet;
        }

        /// <summary>
        /// Gets the pointer to the packet that was read.
        /// </summary>
        public AVPacket* Packet { get; }
    }
}
