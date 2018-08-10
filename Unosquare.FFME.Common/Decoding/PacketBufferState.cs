namespace Unosquare.FFME.Decoding
{
    using System;

    /// <summary>
    /// A value type that representing the packet buffer state.
    /// </summary>
    internal struct PacketBufferState : IEquatable<PacketBufferState>
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

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter;
        ///   otherwise, <see langword="false" />.
        /// </returns>
        public bool Equals(PacketBufferState other) =>
                    Length == other.Length &&
                    Count == other.Count &&
                    CountThreshold == other.CountThreshold &&
                    HasEnoughPackets == other.HasEnoughPackets;
    }
}
