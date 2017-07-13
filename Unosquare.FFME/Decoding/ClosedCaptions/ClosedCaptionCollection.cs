namespace Unosquare.FFME.Decoding.ClosedCaptions
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents a set of Closed Captioning Tracks
    /// in a stream of CC packets.
    /// </summary>
    public class ClosedCaptionCollection
    {

        private int CurrentNtscField = 1;
        private int CurrentChannel = 1;

        /// <summary>
        /// The CC1 Track Packets
        /// </summary>
        public readonly List<ClosedCaptionPacket> CC1 = new List<ClosedCaptionPacket>();

        /// <summary>
        /// The CC2 Track Packets
        /// </summary>
        public readonly List<ClosedCaptionPacket> CC2 = new List<ClosedCaptionPacket>();

        /// <summary>
        /// The CC3 Track Packets
        /// </summary>
        public readonly List<ClosedCaptionPacket> CC3 = new List<ClosedCaptionPacket>();

        /// <summary>
        /// The CC4 Track Packets
        /// </summary>
        public readonly List<ClosedCaptionPacket> CC4 = new List<ClosedCaptionPacket>();

        /// <summary>
        /// Adds the specified packet and automatically places it on the right track.
        /// If the track requires sorting it does so by reordering packets based on their timestamp.
        /// </summary>
        /// <param name="item">The item.</param>
        public void Add(ClosedCaptionPacket item)
        {
            CurrentNtscField = item.NtscField != 0 ? item.NtscField : 1;
            CurrentChannel = item.Channel != 0 ? item.Channel : CurrentChannel;
            var targetCC = CC1;

            if (CurrentNtscField == 1)
            {
                if (CurrentChannel == 1)
                    targetCC = CC1;
                else
                    targetCC = CC2;
            }
            else
            {
                if (CurrentChannel == 1)
                    targetCC = CC3;
                else
                    targetCC = CC4;
            }

            var performSort = targetCC.Count != 0;
            if (targetCC.Count > 0 && targetCC.Last().Timestamp.Ticks <= item.Timestamp.Ticks)
                performSort = false;

            targetCC.Add(item);

            if (performSort)
                targetCC.Sort();

        }
    }
}
