namespace Unosquare.FFME.ClosedCaptions
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents a set of Closed Captioning Tracks
    /// in a stream of CC packets.
    /// </summary>
    public class ClosedCaptionCollection
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClosedCaptionCollection"/> class.
        /// </summary>
        /// <param name="closedCaptions">The closed captions.</param>
        public ClosedCaptionCollection(List<ClosedCaptionPacket> closedCaptions)
        {
            All = closedCaptions;
            var sortedPackets = closedCaptions.OrderBy(p => p.Timestamp.Ticks).ToArray();

            var currentNtscField = 1;
            var currentChannel = 1;

            foreach (var item in sortedPackets)
            {
                currentNtscField = item.NtscField != 0 ? item.NtscField : 1;
                currentChannel = item.Channel != 0 ? item.Channel : currentChannel;
                var targetCC = CC1;

                if (currentNtscField == 1)
                {
                    if (currentChannel == 1)
                        targetCC = CC1;
                    else
                        targetCC = CC2;
                }
                else
                {
                    if (currentChannel == 1)
                        targetCC = CC3;
                    else
                        targetCC = CC4;
                }

                targetCC.Add(item);
            }
        }

        /// <summary>
        /// Gets all the CC packets as originally provided in the constructor.
        /// </summary>
        public List<ClosedCaptionPacket> All { get; }

        /// <summary>
        /// The CC1 Track Packets
        /// </summary>
        public List<ClosedCaptionPacket> CC1 { get; } = new List<ClosedCaptionPacket>(16);

        /// <summary>
        /// The CC2 Track Packets
        /// </summary>
        public List<ClosedCaptionPacket> CC2 { get; } = new List<ClosedCaptionPacket>(16);

        /// <summary>
        /// The CC3 Track Packets
        /// </summary>
        public List<ClosedCaptionPacket> CC3 { get; } = new List<ClosedCaptionPacket>(16);

        /// <summary>
        /// The CC4 Track Packets
        /// </summary>
        public List<ClosedCaptionPacket> CC4 { get; } = new List<ClosedCaptionPacket>(16);
    }
}
