namespace Unosquare.FFME.Rendering
{
    using ClosedCaptions;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class ClosedCaptionsBuffer
    {
        private const int MaxBufferLength = 512;
        private const int ColumnCount = 32;
        private const int RowCount = 15;

        private readonly SortedDictionary<long, ClosedCaptionPacket> PacketBuffer
            = new SortedDictionary<long, ClosedCaptionPacket>();

        private readonly Dictionary<ClosedCaptionChannel, SortedDictionary<long, ClosedCaptionPacket>> ChannelPacketBuffer
            = new Dictionary<ClosedCaptionChannel, SortedDictionary<long, ClosedCaptionPacket>>();

        // Packet State Variables
        private readonly Dictionary<int, Dictionary<int, CellState>> State;
        private ClosedCaptionChannel Channel = ClosedCaptionChannel.CC1;
        private int Parity1LastChannel = 1;
        private int Parity2LastChannel = 1;
        private TimeSpan WriteTag = TimeSpan.MinValue;
        private int CurrentRowIndex = 10; // Default Row Number = 11
        private int CurrentColumnIndex = 0;
        private int ScrollSize = 2; // default is 2

        /// <summary>
        /// Initializes a new instance of the <see cref="ClosedCaptionsBuffer"/> class.
        /// </summary>
        public ClosedCaptionsBuffer()
        {
            for (var channel = 1; channel <= 4; channel++)
                ChannelPacketBuffer[(ClosedCaptionChannel)channel] = new SortedDictionary<long, ClosedCaptionPacket>();

            // Instantiate the state buffer
            State = new Dictionary<int, Dictionary<int, CellState>>(RowCount);
            for (var rowIndex = 0; rowIndex < RowCount; rowIndex++)
            {
                State[rowIndex] = new Dictionary<int, CellState>(ColumnCount);
                for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
                {
                    State[rowIndex][columnIndex] = new CellState(rowIndex, columnIndex);
                }
            }

            // Reset the state completely
            Reset();
        }

        /// <summary>
        /// Renders the packets.
        /// </summary>
        /// <param name="currentBlock">The current block.</param>
        /// <param name="mediaCore">The media core.</param>
        public void Write(VideoBlock currentBlock, MediaEngine mediaCore)
        {
            // Feed the available closed captions into the packet buffer
            // We pre-feed the video blocks to avoid any skipping of CC packets
            // as a result of skipping video frame render calls
            var block = currentBlock;
            while (block != null)
            {
                // Skip the block if we already wrote its CC packets
                if (block.StartTime.Ticks <= WriteTag.Ticks)
                    continue;

                // Add the CC packets to the general packet buffer
                foreach (var cc in block.ClosedCaptions)
                    PacketBuffer[cc.Timestamp.Ticks] = cc;

                // Update the Write Tag and move on to the next block
                WriteTag = block.StartTime;
                block = mediaCore.Blocks[currentBlock.MediaType].Next(block) as VideoBlock;
            }

            // Now, we need to demux the packets from the linear packet buffer
            // into the corresponding independent channel packet buffers
            var maxPosition = currentBlock.EndTime.Ticks; // The maximum demuxer position
            var lastDemuxedKey = long.MinValue; // The demuxer position
            var linearBufferKeys = PacketBuffer.Keys.ToArray();

            foreach (var position in linearBufferKeys)
            {
                // Get a reference to the packet to demux
                var packet = PacketBuffer[position];

                // Stop demuxing packets beyond the current video block
                if (position > maxPosition) break;

                // Update the last processed psoition
                lastDemuxedKey = position;

                // Skip packets that don't have a valid field parity or that are null
                if (packet.FieldParity != 1 && packet.FieldParity != 2) continue;
                if (packet.PacketType == CCPacketType.NullPad || packet.PacketType == CCPacketType.Unrecognized) continue;

                // Update the last channel state if we have all available info
                if (packet.FieldChannel == 1 || packet.FieldChannel == 2)
                {
                    if (packet.FieldParity == 1)
                        Parity1LastChannel = packet.FieldChannel;
                    else
                        Parity2LastChannel = packet.FieldChannel;
                }

                // Compute the channel using the packet's field parity and the last available channel state
                var channel = ClosedCaptionPacket.ComputeChannel(
                    packet.FieldParity, (packet.FieldParity == 1) ? Parity1LastChannel : Parity2LastChannel);

                // Get a reference to the previous packet
                var previousPacket = ChannelPacketBuffer[channel].Count == 0 ?
                    null : ChannelPacketBuffer[channel][ChannelPacketBuffer[channel].Last().Key];

                // Check if the previous packet is just a repeated control code packet; skip it if it is
                if (previousPacket != null && packet.IsRepeatedControlCode(previousPacket))
                    continue;

                // Assign the packet to the correspnding channel buffer
                ChannelPacketBuffer[channel][position] = packet;
            }

            // Remove the demuxed packets from the packet buffer
            foreach (var bufferKey in linearBufferKeys)
            {
                if (bufferKey > lastDemuxedKey)
                    break;

                PacketBuffer.Remove(bufferKey);
            }

            // Trim all buffers to their max length
            TrimBuffers();
        }

        /// <summary>
        /// Resets all the state variables and internal buffers
        /// to their default values.
        /// </summary>
        public void Reset()
        {
            // Clear the packet buffers
            PacketBuffer.Clear();
            for (var channel = 1; channel <= 4; channel++)
                ChannelPacketBuffer[(ClosedCaptionChannel)channel].Clear();

            // Clear the state
            Parity1LastChannel = 1;
            Parity2LastChannel = 1;
            WriteTag = TimeSpan.MinValue;
            CurrentRowIndex = 10; // Default Row Number = 11
            CurrentColumnIndex = 0;
            ScrollSize = 2;

            // Clear the state buffer
            for (var rowIndex = 0; rowIndex < RowCount; rowIndex++)
            {
                for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
                {
                    State[rowIndex][columnIndex].Reset();
                }
            }
        }

        public void Update(ClosedCaptionChannel channel, TimeSpan clockPosition)
        {
            // Reset the buffer state if the channels don't match
            if (channel != Channel)
            {
                Reset();
                Channel = channel;
            }

            // Dequeue packets for all channels but only process the current channel packets
            List<ClosedCaptionPacket> packets = null;
            for (var c = 1; c <= 4; c++)
            {
                var currentChannel = (ClosedCaptionChannel)c;
                var dequeuedPackets = DequeuePackets(ChannelPacketBuffer[currentChannel], clockPosition.Ticks);
                if (currentChannel == Channel)
                    packets = dequeuedPackets;
            }

            if (packets == null) return;
            foreach (var packet in packets)
            {
                if (packet.PacketType == CCPacketType.MiscCommand)
                {
                    if (packet.MiscCommand == CCMiscCommandType.RollUp2)
                    {
                        // TODO
                    }
                }
                else if (packet.PacketType == CCPacketType.Preamble)
                {
                    // TODO
                }
                else if (packet.PacketType == CCPacketType.Text)
                {
                    // TODO
                }
            }
        }

        /// <summary>
        /// Trims all the packet buffers to their maximum allowable length.
        /// </summary>
        private void TrimBuffers()
        {
            // Trim the linear buffer
            TrimBuffer(PacketBuffer);

            // Trim the packet buffer
            for (var channel = 1; channel <= 4; channel++)
                TrimBuffer(ChannelPacketBuffer[(ClosedCaptionChannel)channel]);
        }

        /// <summary>
        /// Trims the packet buffer to the maximum allowable length
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        private void TrimBuffer(SortedDictionary<long, ClosedCaptionPacket> buffer)
        {
            // Don't trim it if we have not reached a maximum length
            if (buffer.Count <= MaxBufferLength)
                return;

            // Find the keys to remove
            var removalCount = buffer.Count - MaxBufferLength;
            var keysToRemove = buffer.Keys.Skip(0).Take(removalCount).ToArray();

            // Remove the target keys
            foreach (var key in keysToRemove)
                buffer.Remove(key);
        }

        /// <summary>
        /// Dequeues the packets from the buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="upToTicks">Up to ticks.</param>
        /// <returns>The dequeued packets, in order.</returns>
        private List<ClosedCaptionPacket> DequeuePackets(SortedDictionary<long, ClosedCaptionPacket> buffer, long upToTicks)
        {
            var result = new List<ClosedCaptionPacket>(buffer.Count);
            var linearBufferKeys = buffer.Keys.ToArray();
            foreach (var bufferKey in linearBufferKeys)
            {
                if (bufferKey > upToTicks)
                    break;

                result.Add(buffer[bufferKey]);
                buffer.Remove(bufferKey);
            }

            return result;
        }

        public class CellState
        {
            public CellState(int rowIndex, int columnIndex)
            {
                RowIndex = rowIndex;
                ColumnIndex = columnIndex;
            }

            public int RowIndex { get; }

            public int ColumnIndex { get; }

            public string Character { get; set; }

            public void Reset()
            {
                Character = null;
            }
        }
    }
}
