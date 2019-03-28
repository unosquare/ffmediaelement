namespace Unosquare.FFME.Container
{
    using System;

    /// <summary>
    /// A value type that representing the packet buffer state.
    /// </summary>
    internal struct PacketBufferState : IEquatable<PacketBufferState>
    {
        /// <summary>
        /// The length in bytes of the packet buffer.
        /// </summary>
        public long Length;

        /// <summary>
        /// The number of packets in the packet buffer.
        /// </summary>
        public int Count;

        /// <summary>
        /// The minimum number of packets so <see cref="HasEnoughPackets"/> is set to true.
        /// </summary>
        public int CountThreshold;

        /// <summary>
        /// Whether the packet buffer has enough packets.
        /// </summary>
        public bool HasEnoughPackets;

        /// <inheritdoc />
        public bool Equals(PacketBufferState other) =>
                    Length == other.Length &&
                    Count == other.Count &&
                    CountThreshold == other.CountThreshold &&
                    HasEnoughPackets == other.HasEnoughPackets;

        /// <inheritdoc />
        public override bool Equals(object obj) =>
            obj is PacketBufferState state && Equals(state);

        /// <inheritdoc />
        public override int GetHashCode() =>
            throw new NotSupportedException($"{nameof(PacketBufferState)} does not support hashing.");
    }
}
