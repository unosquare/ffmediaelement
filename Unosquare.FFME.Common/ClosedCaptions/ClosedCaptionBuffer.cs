namespace Unosquare.FFME.ClosedCaptions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Keeps a sorted list of Closed Caption Packets;
    /// </summary>
    public sealed class ClosedCaptionBuffer
    {
        // TODO: Most likely we'll need to make this  thread-safe
        // TODO: Sample videos with CCs: http://www.pixeltools.com/tech-tip-closed-captioning-existing.html
        private const int MaxCapaciity = 1024;
        private readonly List<ClosedCaptionPacket> Buffer = new List<ClosedCaptionPacket>(MaxCapaciity);

        // private readonly SortedDictionary<long, ClosedCaptionPacket>

        /// <summary>
        /// Initializes a new instance of the <see cref="ClosedCaptionBuffer"/> class.
        /// </summary>
        internal ClosedCaptionBuffer()
        {
            Clear();
        }

        /// <summary>
        /// Gets the log.
        /// </summary>
        public List<string> Log { get; } = new List<string>(2048);

        #region Properties

        /// <summary>
        /// Gets the start time.
        /// </summary>
        public TimeSpan StartTime { get => Buffer.Count > 0 ? Buffer[0].Timestamp : TimeSpan.Zero; }

        /// <summary>
        /// Gets the end time.
        /// </summary>
        public TimeSpan EndTime { get => Buffer.Count > 0 ? Buffer[Buffer.Count - 1].Timestamp : TimeSpan.Zero; }

        /// <summary>
        /// Gets the count.
        /// </summary>
        public int Count { get => Buffer.Count; }

        #endregion

        #region Methods

        /// <summary>
        /// Clears all the packets
        /// </summary>
        public void Clear()
        {
            Buffer.Clear();
        }

        /// <summary>
        /// Adds the specified packets.
        /// </summary>
        /// <param name="packets">The packets.</param>
        public void Add(ICollection<ClosedCaptionPacket> packets)
        {
            foreach (var packet in packets)
            {
                Log.Add(packet.ToString());
            }

            if (packets == null || packets.Count <= 0) return;
            while (Buffer.Count + packets.Count > MaxCapaciity)
                Buffer.RemoveAt(0);

            Buffer.AddRange(packets);
            Buffer.Sort();

            foreach (var packet in packets)
            {
                if (packet.FieldChannel > 0)
                {
                    packet.Channel = ClosedCaptionPacket.ComputeChannel(packet.FieldParity, packet.FieldChannel);
                    continue;
                }

                var previousPacketIndex = Buffer.IndexOf(packet) - 1;
                var previousPacket = packet;
                while (previousPacketIndex >= 0)
                {
                    if (Buffer[previousPacketIndex].FieldParity == packet.FieldParity)
                    {
                        previousPacket = Buffer[previousPacketIndex];
                        break;
                    }

                    previousPacketIndex -= 1;
                }

                packet.Channel = ClosedCaptionPacket.ComputeChannel(packet.FieldParity, previousPacket.FieldChannel);
            }
        }

        /// <summary>
        /// Dequeues closed captions up to the specified timestamp
        /// </summary>
        /// <param name="upTo">Up to.</param>
        /// <returns>A list of closed captions</returns>
        public List<ClosedCaptionPacket> Dequeue(TimeSpan upTo)
        {
            var result = new List<ClosedCaptionPacket>(MaxCapaciity);
            for (var packetIndex = Buffer.Count - 1; packetIndex >= 0; packetIndex--)
            {
                var currentPacket = Buffer[packetIndex];
                if (currentPacket.Timestamp <= upTo)
                {
                    result.Insert(0, currentPacket);
                    Buffer.RemoveAt(packetIndex);
                }
            }

            return result;
        }

        /// <summary>
        /// Dequeues closed captions up to the specified timestamp. Filters items by channel and removes duplicates.
        /// </summary>
        /// <param name="upTo">Up to.</param>
        /// <param name="channel">The channel.</param>
        /// <returns>
        /// A list of closed captions
        /// </returns>
        public List<ClosedCaptionPacket> Dequeue(TimeSpan upTo, ClosedCaptionChannel channel)
        {
            var packets = Dequeue(upTo);

            var channelPackets = new List<ClosedCaptionPacket>(packets.Count);
            foreach (var packet in packets)
            {
                if (packet.Channel == channel)
                    channelPackets.Add(packet);
            }

            return channelPackets;
        }

        #endregion
    }
}
