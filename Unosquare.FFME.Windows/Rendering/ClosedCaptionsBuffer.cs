namespace Unosquare.FFME.Rendering
{
    using ClosedCaptions;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Provides a Closed Captions packet buffer and state manager
    /// </summary>
    internal sealed class ClosedCaptionsBuffer
    {
        #region Constants

        /// <summary>
        /// The column count of the character grid
        /// </summary>
        public const int ColumnCount = 32;

        /// <summary>
        /// The row count of the character grid
        /// </summary>
        public const int RowCount = 15;

        /// <summary>
        /// The maximum length of the individual packet buffers
        /// </summary>
        private const int MaxBufferLength = 512;

        private const int DefaultBaseRowIndex = 10;

        private const int DefaultFieldChannel = 1;

        private const int DefaultScrollSize = 2;

        private const ParserStateMode DefaultStateMode = ParserStateMode.None;

        #endregion

        #region Internal Buffers

        /// <summary>
        /// The linear, non-demuxed packet buffer
        /// </summary>
        private readonly SortedDictionary<long, ClosedCaptionPacket> PacketBuffer
            = new SortedDictionary<long, ClosedCaptionPacket>();

        /// <summary>
        /// The independent channel packet buffers
        /// </summary>
        private readonly Dictionary<CaptionsChannel, SortedDictionary<long, ClosedCaptionPacket>> ChannelPacketBuffer
            = new Dictionary<CaptionsChannel, SortedDictionary<long, ClosedCaptionPacket>>();

        private readonly List<string> DebugData = new List<string>();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ClosedCaptionsBuffer"/> class.
        /// </summary>
        public ClosedCaptionsBuffer()
        {
            for (var channel = 1; channel <= 4; channel++)
                ChannelPacketBuffer[(CaptionsChannel)channel] = new SortedDictionary<long, ClosedCaptionPacket>();

            // Instantiate the state buffer
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

        #endregion

        #region Enumerations

        /// <summary>
        /// Defines the different state parsing modes
        /// </summary>
        public enum ParserStateMode
        {
            /// <summary>
            /// When no state has been detected yet
            /// </summary>
            None,

            /// <summary>
            /// The direct CC display mode
            /// </summary>
            Scrolling,

            /// <summary>
            /// The buffered text display mode
            /// </summary>
            Buffered,

            /// <summary>
            /// The non-display data mode
            /// </summary>
            Data,

            /// <summary>
            /// The XDS, non-display mode
            /// </summary>
            XDS
        }

        #endregion

        #region State Properties

        /// <summary>
        /// Provides access to the state of each of the character cells in the grid
        /// </summary>
        public Dictionary<int, Dictionary<int, CellState>> State { get; } = new Dictionary<int, Dictionary<int, CellState>>(RowCount);

        /// <summary>
        /// Gets the index of the scroll base row.
        /// </summary>
        public int ScrollBaseRowIndex { get; private set; } = DefaultBaseRowIndex;

        /// <summary>
        /// Gets the size of the scroll.
        /// </summary>
        public int ScrollSize { get; private set; } = DefaultScrollSize;

        /// <summary>
        /// Gets a value indicating whether this instance is in buffered mode.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is in buffered mode; otherwise, <c>false</c>.
        /// </value>
        public ParserStateMode StateMode { get; private set; } = DefaultStateMode;

        /// <summary>
        /// Gets the current row index position of the cursor
        /// </summary>
        public int CursorRowIndex { get; private set; } = DefaultBaseRowIndex;

        /// <summary>
        /// Gets the current column index position of the cursor
        /// </summary>
        public int CursorColumnIndex { get; private set; } = default;

        /// <summary>
        /// Gets the currently active packet.
        /// </summary>
        public ClosedCaptionPacket CurrentPacket { get; private set; } = default;

        #endregion

        #region Write State Properties

        /// <summary>
        /// Gets the last start time position of the video block cntaining the CC packets.
        /// </summary>
        public TimeSpan WriteTag { get; private set; } = TimeSpan.MinValue;

        /// <summary>
        /// Gets currently active CC channel.
        /// Changing the channel resets the entire state
        /// </summary>
        public CaptionsChannel Channel { get; private set; } = CaptionsChannel.CC1;

        /// <summary>
        /// Gets the last channel specified by Field with parity 1.
        /// </summary>
        public int Field1LastChannel { get; private set; } = DefaultFieldChannel;

        /// <summary>
        /// Gets the last channel specified by Field with parity 2.
        /// </summary>
        public int Field2LastChannel { get; private set; } = DefaultFieldChannel;

        #endregion

        #region Methods

        /// <summary>
        /// Writes the packets and demuxes them into its independent channel buffers
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
                if (block.ClosedCaptions.Count > 0 && block.StartTime.Ticks > WriteTag.Ticks)
                {
                    // Add the CC packets to the general packet buffer
                    foreach (var cc in block.ClosedCaptions)
                        PacketBuffer[cc.Timestamp.Ticks] = cc;

                    // Update the Write Tag and move on to the next block
                    WriteTag = block.StartTime;
                }

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
                if (packet.PacketType == CaptionsPacketType.NullPad || packet.PacketType == CaptionsPacketType.Unrecognized) continue;

                // Update the last channel state if we have all available info (both parity and channel)
                // This is because some packets will arrive with Field data but not with channel data which means just use the prior channel from the same field.
                if (packet.FieldChannel == 1 || packet.FieldChannel == 2)
                {
                    if (packet.FieldParity == 1)
                        Field1LastChannel = packet.FieldChannel;
                    else
                        Field2LastChannel = packet.FieldChannel;
                }

                // Compute the channel using the packet's field parity and the last available channel state
                var channel = ClosedCaptionPacket.ComputeChannel(
                    packet.FieldParity, (packet.FieldParity == 1) ? Field1LastChannel : Field2LastChannel);

                // Demux the packet to the correspnding channel buffer so the channels are independent
                ChannelPacketBuffer[channel][position] = packet;
            }

            // Remove the demuxed packets from the general (linear) packet buffer
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
            CurrentPacket = default;
            PacketBuffer.Clear();
            for (var channel = 1; channel <= 4; channel++)
                ChannelPacketBuffer[(CaptionsChannel)channel].Clear();

            // Reset the writer state
            Field1LastChannel = DefaultFieldChannel;
            Field2LastChannel = DefaultFieldChannel;
            WriteTag = TimeSpan.MinValue;

            // Reset the parser state
            CursorRowIndex = DefaultBaseRowIndex;
            CursorColumnIndex = default;
            ScrollBaseRowIndex = DefaultBaseRowIndex;
            ScrollSize = DefaultScrollSize;
            StateMode = DefaultStateMode;

            // Clear the state buffer
            for (var rowIndex = 0; rowIndex < RowCount; rowIndex++)
            {
                for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
                {
                    State[rowIndex][columnIndex].Reset();
                }
            }
        }

        /// <summary>
        /// Updates the state using the packets that were demuxed into the specified channel.
        /// This can be called outside the GUI thread.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="clockPosition">The clock position.</param>
        public void UpdateState(CaptionsChannel channel, TimeSpan clockPosition)
        {
            // Reset the buffer state if the channels don't match
            if (channel != Channel)
            {
                Reset();
                Channel = channel;
            }

            if (channel == CaptionsChannel.CCP)
                return;

            // Dequeue packets for all channels but only process the current channel packets
            List<ClosedCaptionPacket> packets = null;
            for (var c = 1; c <= 4; c++)
            {
                var currentChannel = (CaptionsChannel)c;
                var dequeuedPackets = DequeuePackets(ChannelPacketBuffer[currentChannel], clockPosition.Ticks);
                if (currentChannel == Channel)
                    packets = dequeuedPackets;
            }

            // Check if we have at least 1 dequeued packet
            if (packets == null || packets.Count <= 0)
                return;

            // Start processing the dequeued packets for the given channel
            foreach (var packet in packets)
            {
                // Skip duplicated control codes
                if (CurrentPacket != null && CurrentPacket.IsRepeatedControlCode(packet))
                {
                    CurrentPacket = packet;
                    continue;
                }

                // Update the current packet (we need this to detect duplicated control codes)
                CurrentPacket = packet;
                DebugData.Add(packet.ToString());

                // Now, go ahead and process the packet updating the state
                switch (packet.PacketType)
                {
                    case CaptionsPacketType.Charset:
                        {
                            // TODO: Keep a charset state variable
                            break;
                        }

                    case CaptionsPacketType.Color:
                        {
                            // TODO: Keep a color state variable
                            break;
                        }

                    case CaptionsPacketType.MidRow:
                        {
                            // TODO: Keep a midrow style state
                            break;
                        }

                    case CaptionsPacketType.Command:
                        {
                            ProcessCommandPacket(packet);
                            break;
                        }

                    case CaptionsPacketType.Preamble:
                        {
                            ProcessPreamblePacket(packet);
                            break;
                        }

                    case CaptionsPacketType.Tabs:
                        {
                            if (StateMode == ParserStateMode.Scrolling || StateMode == ParserStateMode.Buffered)
                            {
                                CursorColumnIndex += packet.Tabs;
                                CursorColumnIndex = Math.Min(CursorColumnIndex, ColumnCount - 1);
                            }

                            break;
                        }

                    case CaptionsPacketType.Text:
                        {
                            ProcessTextPacket(packet);
                            break;
                        }

                    case CaptionsPacketType.XdsClass:
                        {
                            // Change state back and forth
                            StateMode = packet.XdsClass == CaptionsXdsClass.EndAll ?
                                ParserStateMode.None : ParserStateMode.XDS;
                            break;
                        }

                    case CaptionsPacketType.Unrecognized:
                    case CaptionsPacketType.NullPad:
                    default:
                        {
                            break;
                        }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessCommandPacket(ClosedCaptionPacket packet)
        {
            var command = packet.Command;

            // Set the scroll size if we have a rollup command
            switch (command)
            {
                case CaptionsCommand.RollUp2: ScrollSize = 2; break;
                case CaptionsCommand.RollUp3: ScrollSize = 3; break;
                case CaptionsCommand.RollUp4: ScrollSize = 4; break;
                default: break;
            }

            switch (command)
            {
                case CaptionsCommand.RollUp2:
                case CaptionsCommand.RollUp3:
                case CaptionsCommand.RollUp4:
                    {
                        // Update the state to scrolling
                        StateMode = ParserStateMode.Scrolling;

                        // Clear rows outside of the scrolling area
                        for (var r = 0; r < RowCount; r++)
                        {
                            if (r > ScrollBaseRowIndex - ScrollSize && r <= ScrollBaseRowIndex)
                                continue;

                            for (var c = 0; c < ColumnCount; c++)
                                State[r][c].ClearDisplay();
                        }

                        break;
                    }

                case CaptionsCommand.NewLine:
                    {
                        if (StateMode == ParserStateMode.Scrolling)
                        {
                            var targetRowIndex = CursorRowIndex - 1;
                            for (var c = 0; c < ColumnCount; c++)
                            {
                                State[targetRowIndex][c].Display = State[CursorRowIndex][c].Display;
                                State[CursorRowIndex][c].ClearDisplay();
                            }

                            CursorRowIndex = ScrollBaseRowIndex;
                            CursorColumnIndex = default;
                        }

                        break;
                    }

                case CaptionsCommand.Resume:
                    {
                        StateMode = ParserStateMode.Buffered;
                        CursorRowIndex = 0;
                        CursorColumnIndex = 0;
                        break;
                    }

                case CaptionsCommand.ClearScreen:
                    {
                        for (var r = 0; r < RowCount; r++)
                        {
                            for (var c = 0; c < ColumnCount; c++)
                            {
                                State[r][c].ClearDisplay();
                            }
                        }

                        break;
                    }

                case CaptionsCommand.EndCaption:
                    {
                        StateMode = ParserStateMode.None;
                        CursorRowIndex = 0;
                        CursorColumnIndex = 0;

                        for (var r = 0; r < RowCount; r++)
                        {
                            for (var c = 0; c < ColumnCount; c++)
                            {
                                State[r][c].DisplayBuffer();
                            }
                        }

                        break;
                    }

                case CaptionsCommand.AlarmOff:
                case CaptionsCommand.AlarmOn:
                case CaptionsCommand.None:
                default:
                    {
                        break;
                    }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessPreamblePacket(ClosedCaptionPacket packet)
        {
            if (StateMode == ParserStateMode.Scrolling || StateMode == ParserStateMode.Buffered)
            {
                ScrollBaseRowIndex = packet.PreambleRow - 1;
                CursorRowIndex = ScrollBaseRowIndex;
                CursorColumnIndex = default;

                // Apply indenting if needed
                switch (packet.PreambleStyle)
                {
                    case CaptionsStyle.WhiteIndent0:
                    case CaptionsStyle.WhiteIndent0Underline:
                        {
                            CursorColumnIndex = 0;
                            break;
                        }

                    case CaptionsStyle.WhiteIndent4:
                    case CaptionsStyle.WhiteIndent4Underline:
                        {
                            CursorColumnIndex = 3;
                            break;
                        }

                    case CaptionsStyle.WhiteIndent8:
                    case CaptionsStyle.WhiteIndent8Underline:
                        {
                            CursorColumnIndex = 7;
                            break;
                        }

                    case CaptionsStyle.WhiteIndent12:
                    case CaptionsStyle.WhiteIndent12Underline:
                        {
                            CursorColumnIndex = 11;
                            break;
                        }

                    case CaptionsStyle.WhiteIndent16:
                    case CaptionsStyle.WhiteIndent16Underline:
                        {
                            CursorColumnIndex = 15;
                            break;
                        }

                    case CaptionsStyle.WhiteIndent20:
                    case CaptionsStyle.WhiteIndent20Underline:
                        {
                            CursorColumnIndex = 19;
                            break;
                        }

                    case CaptionsStyle.WhiteIndent24:
                    case CaptionsStyle.WhiteIndent24Underline:
                        {
                            CursorColumnIndex = 23;
                            break;
                        }

                    case CaptionsStyle.WhiteIndent28:
                    case CaptionsStyle.WhiteIndent28Underline:
                        {
                            CursorColumnIndex = 27;
                            break;
                        }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessTextPacket(ClosedCaptionPacket packet)
        {
            if (StateMode == ParserStateMode.Scrolling || StateMode == ParserStateMode.Buffered)
            {
                var offset = 0;
                for (var c = CursorColumnIndex; c < ColumnCount; c++)
                {
                    if (offset > packet.Text.Length - 1) break;
                    if (StateMode == ParserStateMode.Scrolling)
                        State[CursorRowIndex][c].Display = packet.Text[offset];
                    else
                        State[CursorRowIndex][c].Buffer = packet.Text[offset];

                    offset++;
                }

                CursorColumnIndex += offset;
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
                TrimBuffer(ChannelPacketBuffer[(CaptionsChannel)channel]);
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

        #endregion

        #region Supporting Classes

        /// <summary>
        /// Represents a grid cell state containing a signle character of text
        /// </summary>
        public sealed class CellState
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CellState" /> class.
            /// </summary>
            /// <param name="rowIndex">Index of the row.</param>
            /// <param name="columnIndex">Index of the column.</param>
            public CellState(int rowIndex, int columnIndex)
            {
                RowIndex = rowIndex;
                ColumnIndex = columnIndex;
            }

            /// <summary>
            /// Gets the index of the row this cell belongs to.
            /// </summary>
            public int RowIndex { get; }

            /// <summary>
            /// Gets the index of the column this cell belongs to.
            /// </summary>
            public int ColumnIndex { get; }

            /// <summary>
            /// Gets the display character as a string
            /// </summary>
            public string Text => Display == default ? string.Empty : Display.ToString();

            /// <summary>
            /// Gets or sets the character.
            /// </summary>
            public char Display { get; set; }

            /// <summary>
            /// Gets or sets the buffered character.
            /// </summary>
            public char Buffer { get; set; }

            public void DisplayBuffer()
            {
                Display = Buffer;
                Buffer = default;
            }

            /// <summary>
            /// Resets the entire state and contents of this cell
            /// </summary>
            public void Reset()
            {
                ClearDisplay();
                ClearBuffer();
            }

            /// <summary>
            /// Clears the character contents of this cell
            /// </summary>
            public void ClearDisplay()
            {
                Display = default;
            }

            public void ClearBuffer()
            {
                Buffer = default;
            }
        }

        #endregion
    }
}
