namespace Unosquare.FFME.Decoding
{
    /// <summary>
    /// A value type that representing the packet buffer state.
    /// </summary>
    internal struct PacketBufferState
    {
        /// <summary>
        /// The length in bytes of the packet buffer
        /// </summary>
        public long Length;

        /// <summary>
        /// The number of packets in the packet buffer
        /// </summary>
        public int Count;

        /// <summary>
        /// The minimum number of packets so <see cref="HasEnoughPackets"/> is set to true.
        /// </summary>
        public int CountThreshold;

        /// <summary>
        /// Thether the packet buffer has enough packets
        /// </summary>
        public bool HasEnoughPackets;
    }
}
