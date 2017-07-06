namespace Unosquare.FFME
{
    using Core;
    using Decoding;
    using Rendering;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Controls;
    using System.Windows.Threading;

    partial class MediaElement
    {
        /// <summary>
        /// This partial class implements: 
        /// 1. Packet reading from the Container
        /// 2. Frame Decoding from packet buffer
        /// 3. Block Rendering from frame queue
        /// </summary>

        #region Constants

        internal static readonly Dictionary<MediaType, int> MaxBlocks
            = new Dictionary<MediaType, int>()
        {
            { MediaType.Video, 12 },
            { MediaType.Audio, 24 },
            { MediaType.Subtitle, 48 }
        };

        private static readonly Dictionary<MediaType, int> MaxFrames
            = new Dictionary<MediaType, int>()
        {
            { MediaType.Video, 24 },
            { MediaType.Audio, 48 },
            { MediaType.Subtitle, 48 }
        };

        #endregion

        #region State Variables

        internal readonly MediaTypeDictionary<MediaFrameQueue> Frames
            = new MediaTypeDictionary<MediaFrameQueue>();

        internal readonly MediaTypeDictionary<MediaBlockBuffer> Blocks
            = new MediaTypeDictionary<MediaBlockBuffer>();

        internal readonly MediaTypeDictionary<IRenderer> Renderers
            = new MediaTypeDictionary<IRenderer>();

        internal readonly MediaTypeDictionary<TimeSpan> LastRenderTime
            = new MediaTypeDictionary<TimeSpan>();

        internal volatile bool IsTaskCancellationPending = false;


        internal Thread PacketReadingTask;
        internal readonly ManualResetEvent PacketReadingCycle = new ManualResetEvent(true);

        internal Thread FrameDecodingTask;
        internal readonly ManualResetEvent FrameDecodingCycle = new ManualResetEvent(true);

        internal Thread BlockRenderingTask;
        internal readonly ManualResetEvent BlockRenderingCycle = new ManualResetEvent(true);

        internal readonly ManualResetEvent SeekingDone = new ManualResetEvent(true);

        #endregion

        #region Private Properties

        /// <summary>
        /// Gets a value indicating whether more packets can be read from the stream.
        /// This does not check if the packet queue is full.
        /// </summary>
        private bool CanReadMorePackets { get { return Container.IsAtEndOfStream == false; } }

        /// <summary>
        /// Gets a value indicating whether more frames can be decoded from the packet queue.
        /// That is, if we have packets in the packet buffer or if we are not at the end of the stream.
        /// </summary>
        private bool CanReadMoreFrames { get { return CanReadMorePackets || Container.Components.PacketBufferLength > 0; } }

        /// <summary>
        /// Gets a value indicating whether more frames can be converted into blocks.
        /// </summary>
        private bool CanReadMoreBlocks { get { return CanReadMoreFrames || Frames.Any(f => f.Value.Count > 0); } }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a value indicating whether more frames can be converted into blocks of the given type.
        /// </summary>
        private bool CanReadMoreBlocksOf(MediaType t) { return CanReadMoreFrames || Frames[t].Count > 0; }

        /// <summary>
        /// Dequeues the next available frame and converts it into a block of the appropriate type,
        /// adding it to the correpsonding block buffer. If there is no more blocks in the pool, then 
        /// more room is provided automatically.
        /// </summary>
        /// <param name="t">The media type.</param>
        private MediaBlock AddNextBlock(MediaType t)
        {
            var frame = Frames[t].Dequeue();
            if (frame == null)
                return null;

            var addedBlock = Blocks[t].Add(frame, Container);
            return addedBlock;
        }

        /// <summary>
        /// Buffers some packets which in turn get decoded into frames and then
        /// converted into blocks.
        /// </summary>
        /// <param name="packetBufferLength">Length of the packet buffer.</param>
        /// <param name="clearExisting">if set to <c>true</c> clears the existing frames and blocks.</param>
        private void BufferBlocks(int packetBufferLength, bool clearExisting)
        {
            var main = Container.Components.Main.MediaType;

            // Clear Blocks and frames, reset the render times
            if (clearExisting)
                foreach (var t in Container.Components.MediaTypes)
                {
                    Blocks[t].Clear();
                    LastRenderTime[t] = TimeSpan.MinValue;
                }

            // Raise the buffering started event.
            IsBuffering = true;
            BufferingProgress = 0;
            RaiseBufferingStartedEvent();

            // Buffer some packets
            while (CanReadMorePackets && Container.Components.PacketBufferLength < packetBufferLength)
                PacketReadingCycle.WaitOne();

            // Buffer some blocks
            while (CanReadMoreBlocks && Blocks[main].CapacityPercent <= 0.5d)
            {
                PacketReadingCycle.WaitOne();
                FrameDecodingCycle.WaitOne();
                BufferingProgress = Blocks[main].CapacityPercent / 0.5d;
                foreach (var t in Container.Components.MediaTypes)
                    AddNextBlock(t);
            }

            // Raise the buffering started event.
            BufferingProgress = 1;
            IsBuffering = false;
            RaiseBufferingEndedEvent();
        }

        /// <summary>
        /// The render block callback that updates the reported media position
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <param name="renderIndex">Index of the render.</param>
        private void RenderBlock(MediaBlock block, TimeSpan clockPosition, int renderIndex)
        {
            Renderers[block.MediaType].Render(block, clockPosition, renderIndex);
            this.LogRenderBlock(block, clockPosition, renderIndex);
        }

        #endregion

        #region Packet Reading Worker

        /// <summary>
        /// Runs the read task which keeps a packet buffer as full as possible.
        /// It reports on DownloadProgress by enqueueing an update to the property
        /// in order to avoid any kind of disruption to this thread caused by the UI thread.
        /// </summary>
        internal async void RunPacketReadingWorker()
        {
            var packetsRead = 0;

            while (IsTaskCancellationPending == false)
            {
                // Enter a read cycle
                SeekingDone.WaitOne();
                PacketReadingCycle.Reset();
                //Container.Log(MediaLogMessageType.Debug, "RESET");
                // Read a bunch of packets at a time
                packetsRead = 0;
                while (Container.Components.PacketBufferLength < DownloadCacheLength
                    && packetsRead < Constants.PacketReadBatchCount
                    && CanReadMorePackets)
                {
                    Container.Read();
                    packetsRead++;
                }

                // finish the reading cycle.
                PacketReadingCycle.Set();

                //Container.Log(MediaLogMessageType.Debug, "SET");
                // Wait some if we have a full packet buffer or we are unable to read more packets.
                if (Container.Components.PacketBufferLength >= DownloadCacheLength || CanReadMorePackets == false)
                    await Task.Delay(1);
            }

            PacketReadingCycle.Set();
        }

        #endregion

        #region Frame Decoding Worker

        /// <summary>
        /// Continually decodes the available packet buffer to have as
        /// many frames as possible in each frame queue and 
        /// up to the MaxFrames on each component
        /// </summary>
        internal async void RunFrameDecodingWorker()
        {
            var decodedFrames = 0;

            while (IsTaskCancellationPending == false)
            {
                // Wait for a seek operation to complete (if any)
                // and initiate a frame decoding cycle.
                SeekingDone.WaitOne();
                FrameDecodingCycle.Reset();

                // Decode Frames if necessary
                decodedFrames = 0;

                // Decode frames for each of the components
                foreach (var component in Container.Components.All)
                {
                    // Check if we can accept more frames
                    if (Frames[component.MediaType].Count >= MaxFrames[component.MediaType])
                        continue;

                    // Don't do anything if we don't have packets to decode
                    if (component.PacketBufferCount <= 0)
                        continue;

                    // Push the decoded frames
                    var frames = component.DecodeNextPacket();
                    foreach (var frame in frames)
                    {
                        Frames[frame.MediaType].Push(frame);
                        decodedFrames += 1;
                    }
                }

                // Complete the frame decoding cycle
                FrameDecodingCycle.Set();

                // Give it a break if there was nothing to decode.
                if (decodedFrames <= 0)
                    await Task.Delay(1);

            }

            FrameDecodingCycle.Set();

        }

        #endregion

        #region Block Rendering Worker

        /// <summary>
        /// Continuously converts frmes and places them on the corresponding
        /// block buffer. This task is responsible for keeping track of the clock
        /// and calling the render methods appropriate for the current clock position.
        /// </summary>
        internal async void RunBlockRenderingWorker()
        {
            #region 0. Initialize Running State

            // Holds the main media type
            var main = Container.Components.Main.MediaType;
            // Holds the auxiliary media types
            var auxs = Container.Components.MediaTypes.Where(t => t != main).ToArray();
            // Holds all components
            var all = Container.Components.MediaTypes.ToArray();

            // Create and reset all the tracking variables
            var hasRendered = new MediaTypeDictionary<bool>();
            var renderIndex = new MediaTypeDictionary<int>();
            var renderBlock = new MediaTypeDictionary<MediaBlock>();

            // reset all state variables for all components
            foreach (var t in all)
            {
                hasRendered[t] = false;
                renderIndex[t] = -1;
                renderBlock[t] = null;
                LastRenderTime[t] = TimeSpan.MinValue;
            }

            // Buffer some blocks and adjust the clock to the start position
            BufferBlocks(BufferCacheLength, false);
            Clock.Position = Blocks[main].RangeStartTime;
            var wallClock = Clock.Position;

            #endregion

            while (true)
            {
                #region 1. Control and Capture

                // Execute commands at the beginning of the cycle
                while (Commands.PendingCount > 0)
                    await Commands.ProcessNext();

                // Check if one of the commands has requested an exit
                if (IsTaskCancellationPending) break;

                // Capture current clock position for the rest of this cycle
                BlockRenderingCycle.Reset();
                wallClock = Clock.Position;

                #endregion

                #region 2. Handle Main Component

                // Reset the hasRendered tracker
                hasRendered[main] = false;

                // Check for out-of sync issues (i.e. after seeking), being cautious about EOF/media ended scenarios
                // in which more blocks cannot be read. (The clock is on or beyond the Duration)
                if ((Blocks[main].Count <= 0 || Blocks[main].IsInRange(wallClock) == false) && CanReadMoreBlocksOf(main))
                {
                    BufferBlocks(BufferCacheLength, true);
                    wallClock = Blocks[main].IsInRange(wallClock) ? wallClock : Blocks[main].RangeStartTime;
                    Container.Logger?.Log(MediaLogMessageType.Warning, $"SYNC CLOCK: {Clock.Position.Format()} | TGT: {wallClock.Format()}");
                    Clock.Position = wallClock;
                    LastRenderTime[main] = TimeSpan.MinValue;

                    // a forced sync is basically a seek operation.
                    foreach (var t in all) Renderers[t].Seek();
                }

                // capture the render block based on its index
                if ((renderIndex[main] = Blocks[main].IndexOf(wallClock)) >= 0)
                {
                    renderBlock[main] = Blocks[main][renderIndex[main]];

                    // render the frame if we have not rendered it
                    if ((renderBlock[main].StartTime != LastRenderTime[main] || LastRenderTime[main] == TimeSpan.MinValue)
                        && (IsPlaying == false || wallClock.Ticks >= renderBlock[main].StartTime.Ticks))
                    {
                        // Record the render time
                        LastRenderTime[main] = renderBlock[main].StartTime;

                        // Send the block to the renderer
                        RenderBlock(renderBlock[main], wallClock, renderIndex[main]);
                        hasRendered[main] = true;
                    }
                }

                #endregion

                #region 3. Handle Auxiliary Components

                // Render each of the Media Types if it is time to do so.
                foreach (var t in auxs)
                {
                    hasRendered[t] = false;

                    // Extract the render index
                    renderIndex[t] = Blocks[t].IndexOf(wallClock);

                    // If it's a secondary stream, try to catch up with the primary stream as quickly as possible
                    // by skipping the queued blocks and adding new ones as quickly as possible.
                    while (Blocks[t].RangeEndTime <= Blocks[main].RangeStartTime
                        && renderIndex[t] >= Blocks[t].Count - 1
                        && CanReadMoreBlocksOf(t))
                    {
                        if (AddNextBlock(t) == null) break;
                        renderIndex[t] = Blocks[t].IndexOf(wallClock);
                        LastRenderTime[t] = TimeSpan.MinValue;
                    }

                    // capture the latest renderindex
                    renderIndex[t] = Blocks[t].IndexOf(wallClock);

                    // Skip to next stream component if we have nothing left to do here :(
                    if (renderIndex[t] < 0) continue;

                    // Retrieve the render block
                    renderBlock[t] = Blocks[t][renderIndex[t]];

                    // render the frame if we have not rendered
                    if ((renderBlock[t].StartTime != LastRenderTime[t] || LastRenderTime[t] == TimeSpan.MinValue)
                        && (IsPlaying == false || wallClock.Ticks >= renderBlock[t].StartTime.Ticks))
                    {
                        LastRenderTime[t] = renderBlock[t].StartTime;
                        RenderBlock(renderBlock[t], wallClock, renderIndex[t]);
                        hasRendered[t] = true;
                    }
                }

                #endregion

                #region 4. Keep Blocks Buffered

                foreach (var t in all)
                {
                    if (hasRendered[t] == false) continue;

                    // Add the next block if the conditions require us to do so:
                    // If rendered, then we need to discard the oldest and add the newest
                    // If the render index is greater than half, the capacity, add a new block
                    while (Blocks[t].IsFull == false || renderIndex[t] + 1 > Blocks[t].Capacity / 2)
                    {
                        if (AddNextBlock(t) == null) break;
                        renderIndex[t] = Blocks[t].IndexOf(wallClock);
                    }

                    hasRendered[t] = false;
                    renderIndex[t] = Blocks[t].IndexOf(wallClock);
                }

                #endregion

                #region 5. Detect End of Media

                // Detect end of block rendering
                if (CanReadMoreBlocksOf(main) == false && renderIndex[main] == Blocks[main].Count - 1)
                {
                    if (HasMediaEnded == false)
                    {
                        // Rendered all and nothing else to read
                        Clock.Pause();
                        Clock.Position = NaturalDuration.HasTimeSpan ?
                            NaturalDuration.TimeSpan : Blocks[main].RangeEndTime;
                        MediaState = MediaState.Pause;
                        UpdatePosition(Clock.Position);
                        HasMediaEnded = true;
                        RaiseMediaEndedEvent();
                    }
                }
                else
                {
                    HasMediaEnded = false;
                }

                #endregion

                #region 6. Finalize the Rendering Cycle

                BlockRenderingCycle.Set();

                // Pause for a bit if we have no more commands to process.
                if (Commands.PendingCount <= 0)
                    await Task.Delay(1);

                #endregion
            }

            BlockRenderingCycle.Set();

        }

        #endregion

    }
}
