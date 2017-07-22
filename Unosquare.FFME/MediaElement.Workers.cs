﻿namespace Unosquare.FFME
{
    using Commands;
    using Core;
    using Decoding;
    using Rendering;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Controls;

    partial class MediaElement
    {
        /// <summary>
        /// This partial class implements: 
        /// 1. Packet reading from the Container
        /// 2. Frame Decoding from packet buffer and Block buffering
        /// 3. Block Rendering from block buffer
        /// </summary>

        #region Constants

        // TODO: Make this configurable
        internal static readonly Dictionary<MediaType, int> MaxBlocks = new Dictionary<MediaType, int>
        {
            { MediaType.Video, 12 },
            { MediaType.Audio, 120 },
            { MediaType.Subtitle, 120 }
        };

        #endregion

        #region State Variables

        internal readonly MediaTypeDictionary<MediaBlockBuffer> Blocks
            = new MediaTypeDictionary<MediaBlockBuffer>();

        internal readonly MediaTypeDictionary<IRenderer> Renderers
            = new MediaTypeDictionary<IRenderer>();

        internal readonly MediaTypeDictionary<TimeSpan> LastRenderTime
            = new MediaTypeDictionary<TimeSpan>();

        internal volatile bool IsTaskCancellationPending = false;
        internal volatile bool HasDecoderSeeked = false;

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
        private bool CanReadMorePackets { get { return (Container?.IsAtEndOfStream ?? true) == false; } }

        /// <summary>
        /// Gets a value indicating whether more frames can be decoded from the packet queue.
        /// That is, if we have packets in the packet buffer or if we are not at the end of the stream.
        /// </summary>
        private bool CanReadMoreFrames { get { return CanReadMorePackets || Container.Components.PacketBufferLength > 0; } }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a value indicating whether more frames can be converted into blocks of the given type.
        /// </summary>
        private bool CanReadMoreFramesOf(MediaType t) { return CanReadMorePackets || Container.Components[t].PacketBufferLength > 0; }

        /// <summary>
        /// Sends the given block to its corresponding media renderer.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        private void SendBlockToRenderer(MediaBlock block, TimeSpan clockPosition)
        {
            Renderers[block.MediaType].Render(block, clockPosition);
            this.LogRenderBlock(block, clockPosition, Blocks[block.MediaType].IndexOf(clockPosition));
        }

        /// <summary>
        /// Adds the blocks of the given media type.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns></returns>
        private int AddBlocks(MediaType t)
        {
            var decodedFrameCount = 0;

            // Decode the frames
            var frames = Container.Components[t].ReceiveFrames();

            // exit the loop if there was nothing more to decode
            foreach (var frame in frames)
            {
                // Add each decoded frame as a playback block
                if (frame == null) continue;
                Blocks[t].Add(frame, Container);
                decodedFrameCount += 1;
            }

            return decodedFrameCount;
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
            // Holds the packet count for each read cycle
            var packetsRead = new MediaTypeDictionary<int>();

            // State variables for media types
            var t = MediaType.None;
            var main = Container.Components.Main.MediaType;
            var auxs = Container.Components.MediaTypes.Where(c => c != main && (c == MediaType.Audio || c == MediaType.Video)).ToArray();
            var all = auxs.Union(new[] { main }).ToArray();

            // State variables for bytes read (give-up condition)
            var startBytesRead = 0UL;
            var currentBytesRead = 0UL;

            // Worker logic begins here
            while (IsTaskCancellationPending == false)
            {
                // Enter a read cycle
                SeekingDone.WaitOne();
                PacketReadingCycle.Reset();

                if (CanReadMorePackets && Container.Components.PacketBufferLength < DownloadCacheLength)
                {
                    // Initialize Packets read to 0 for each component and state variables
                    foreach (var k in Container.Components.MediaTypes)
                        packetsRead[k] = 0;

                    startBytesRead = Container.Components.TotalBytesRead;
                    currentBytesRead = 0UL;

                    // Start to perform the read loop
                    while (CanReadMorePackets)
                    {
                        // Perform a packet read. t will hold the packet type.
                        t = Container.Read();

                        // Discard packets that we don't need (i.e. MediaType == None)
                        if (Container.Components.MediaTypes.Contains(t) == false)
                            continue;

                        // Update the packet count for the components
                        packetsRead[t] += 1;

                        // Ensure we have read at least some packets from main and auxiliary streams.
                        if (packetsRead.Where(k => all.Contains(k.Key)).All(c => c.Value > 0))
                            break;

                        // The give-up condition is that in spite of efforts to read at least one of each,
                        // we could not find the required packet types.
                        currentBytesRead = Container.Components.TotalBytesRead - startBytesRead;
                        if (currentBytesRead > (ulong)DownloadCacheLength)
                            break;
                    }
                }

                // finish the reading cycle.
                PacketReadingCycle.Set();

                // Simply exit the thread when cancellation has been requested
                if (IsTaskCancellationPending) break;

                // Wait some if we have a full packet buffer or we are unable to read more packets (i.e. EOF).
                if (Container.Components.PacketBufferLength >= DownloadCacheLength || CanReadMorePackets == false)
                    await Task.Delay(1);
            }

            // Always exit notifying the reading cycle is done.
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
            var decodedFrameCount = 0;

            var wallClock = TimeSpan.Zero;
            var rangePercent = 0d;
            var isInRange = false;

            // Holds the main media type
            var main = Container.Components.Main.MediaType;
            // Holds the auxiliary media types
            var auxs = Container.Components.MediaTypes.Where(x => x != main).ToArray();
            // Holds all components
            var all = Container.Components.MediaTypes.ToArray();

            var isBuffering = false;
            var resumeClock = false;
            var hasPendingSeeks = false;

            MediaComponent comp = null;
            MediaBlockBuffer blocks = null;

            while (IsTaskCancellationPending == false)
            {
                #region 1. Setup the Decoding Cycle

                hasPendingSeeks = Commands.PendingCountOf(MediaCommandType.Seek) > 0;
                if (IsSeeking == false && hasPendingSeeks)
                {
                    IsSeeking = true;
                    RaiseSeekingStartedEvent();
                }

                // Execute the following command at the beginning of the cycle
                await Commands.ProcessNext();

                hasPendingSeeks = Commands.PendingCountOf(MediaCommandType.Seek) > 0;
                if (IsSeeking == true && hasPendingSeeks == false)
                {
                    IsSeeking = false;

                    // Call the seek method on all renderers
                    foreach (var kvp in Renderers)
                        kvp.Value.Seek();

                    RaiseSeekingEndedEvent();
                }

                // Check if one of the commands has requested an exit
                if (IsTaskCancellationPending) break;

                // Wait for a seek operation to complete (if any)
                // and initiate a frame decoding cycle.
                SeekingDone.WaitOne();
                FrameDecodingCycle.Reset();

                // Set initial state
                wallClock = Clock.Position;
                decodedFrameCount = 0;

                #endregion

                #region 2. Main Component Decoding

                // Capture component and blocks for easier readability
                comp = Container.Components[main];
                blocks = Blocks[main];

                // Handle the main component decoding; Start by checking we have some packets
                if (comp.PacketBufferCount > 0)
                {
                    // Detect if we are in range for the main component
                    isInRange = blocks.IsInRange(wallClock);

                    if (isInRange == false)
                    {
                        // Clear the media blocks if we are outside of the required range
                        // we don't need them and we now need as many playback blocks as we can have available
                        if (blocks.IsFull)
                            blocks.Clear();

                        // detect a buffering scenario
                        if (blocks.Count <= 0)
                        {
                            HasDecoderSeeked = true;
                            isBuffering = true;
                            resumeClock = Clock.IsRunning;
                            Clock.Pause();
                            Logger.Log(MediaLogMessageType.Debug, $"SYNC BUFFER: Buffering Started.");
                        }

                        // Read some frames and try to get a valid range
                        while (comp.PacketBufferCount > 0 && blocks.IsFull == false)
                        {
                            decodedFrameCount = AddBlocks(main);
                            isInRange = blocks.IsInRange(wallClock);
                            if (isInRange)
                                break;

                            // Try to get more packets by waiting for read cycles.
                            if (CanReadMorePackets && comp.PacketBufferCount <= 0 && isInRange == false)
                                PacketReadingCycle.WaitOne();
                        }

                        // Unfortunately at this point we will need to adjust the clock after creating the frames.
                        // to ensure tha mian component is within the clock range if the decoded
                        // frames are not with range. This is normal while buffering though.
                        if (isInRange == false)
                        {
                            wallClock = wallClock <= blocks.RangeStartTime ?
                                blocks.RangeStartTime : blocks.RangeEndTime;

                            if (isBuffering == false)
                                Logger.Log(MediaLogMessageType.Warning, $"SYNC CLOCK: {Clock.Position.Format()} set to {wallClock.Format()}");

                            // Update the clock to what the main component range mandates
                            Clock.Position = wallClock;
                        }
                    }
                    else
                    {
                        // Check if we need more blocks for the current components
                        rangePercent = blocks.GetRangePercent(wallClock);

                        // Read as many blocks as we possibly can
                        while (comp.PacketBufferCount > 0 &&
                            ((rangePercent > 0.75d && blocks.IsFull) || blocks.IsFull == false))
                        {
                            decodedFrameCount = AddBlocks(main);
                            rangePercent = blocks.GetRangePercent(wallClock);
                        }
                    }

                }

                #endregion

                #region 3. Auxiliary Component Decoding

                foreach (var t in auxs)
                {
                    if (IsSeeking) continue;

                    // Capture the current block buffer and component
                    // for easier readability
                    comp = Container.Components[t];
                    blocks = Blocks[t];

                    // wait for component to get there if we only have furutre blocks
                    // in auxiliary component.
                    if (blocks.RangeStartTime > wallClock)
                        continue;

                    // Wait for packets if we are buffering or we don't have enough packets
                    if (CanReadMorePackets && (isBuffering || comp.PacketBufferCount <= 0))
                        PacketReadingCycle.WaitOne();

                    // catch up with the wall clock
                    while (comp.PacketBufferCount > 0 && blocks.RangeEndTime <= wallClock)
                    {
                        decodedFrameCount = AddBlocks(t);
                        // don't care if we are buffering
                        // always try to catch up by reading more packets.
                        if (comp.PacketBufferCount <= 0 && CanReadMorePackets)
                            PacketReadingCycle.WaitOne();
                    }

                    rangePercent = blocks.GetRangePercent(wallClock);
                    isInRange = blocks.IsInRange(wallClock);

                    // Wait for packets if we are buffering
                    if (CanReadMorePackets && isBuffering)
                        PacketReadingCycle.WaitOne();

                    while (comp.PacketBufferCount > 0 &&
                        (
                            (blocks.IsFull == true && isInRange && rangePercent > 0.75d && rangePercent < 1d) ||
                            (blocks.IsFull == false)
                        ))
                    {
                        decodedFrameCount = AddBlocks(t);
                        rangePercent = blocks.GetRangePercent(wallClock);
                        isInRange = blocks.IsInRange(wallClock);

                        if (CanReadMorePackets && isBuffering)
                            PacketReadingCycle.WaitOne();
                    }
                }

                #endregion

                #region 4. Detect End of Media

                // Detect end of block rendering
                if (isBuffering == false
                    && IsSeeking == false
                    && CanReadMoreFramesOf(main) == false
                    && Blocks[main].IndexOf(wallClock) == Blocks[main].Count - 1)
                {
                    if (HasMediaEnded == false)
                    {
                        // Rendered all and nothing else to read
                        Clock.Pause();
                        Clock.Position = NaturalDuration.HasTimeSpan ?
                            NaturalDuration.TimeSpan : Blocks[main].RangeEndTime;
                        wallClock = Clock.Position;

                        HasMediaEnded = true;
                        MediaState = MediaState.Pause;
                        RaiseMediaEndedEvent();
                    }
                }
                else
                {
                    HasMediaEnded = false;
                }

                #endregion

                #region 6. Finish the Cycle

                // complete buffering notifications
                if (isBuffering)
                {
                    isBuffering = false;
                    if (resumeClock) Clock.Play();
                    Logger.Log(MediaLogMessageType.Debug, $"SYNC BUFFER: Buffering Finished. Clock set to {wallClock.Format()}");
                }

                // Complete the frame decoding cycle
                FrameDecodingCycle.Set();

                // After a seek operation, always reset the has seeked flag.
                HasDecoderSeeked = false;

                // Simply exit the thread when cancellation has been requested
                if (IsTaskCancellationPending) break;

                // Give it a break if there was nothing to decode.
                // We probably need to wait for some more input
                if (decodedFrameCount <= 0 && Commands.PendingCount <= 0)
                    await Task.Delay(1);

                #endregion
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
            // Holds a snapshot of the current block to render
            var currentBlock = new MediaTypeDictionary<MediaBlock>();

            var renderedBlockCount = 0;

            // reset render times for all components
            foreach (var t in all)
                LastRenderTime[t] = TimeSpan.MinValue;

            // Ensure the other workers are running
            PacketReadingCycle.WaitOne();
            FrameDecodingCycle.WaitOne();

            // Set the initial clock position
            Clock.Position = Blocks[main].RangeStartTime;
            var wallClock = Clock.Position;

            #endregion

            while (IsTaskCancellationPending == false)
            {
                renderedBlockCount = 0;

                #region 1. Control and Capture

                // Check if one of the commands has requested an exit
                if (IsTaskCancellationPending) break;

                // Capture current clock position for the rest of this cycle
                BlockRenderingCycle.Reset();

                // capture the wall clock for this cycle
                wallClock = Clock.Position;

                #endregion

                #region 2. Handle Block Rendering

                // Capture the blocks to render
                foreach (var t in all)
                    currentBlock[t] = HasDecoderSeeked ? null : Blocks[t][wallClock];

                // Render each of the Media Types if it is time to do so.
                foreach (var t in all)
                {
                    if (currentBlock[t] == null)
                        continue;

                    // render the frame if we have not rendered
                    if ((currentBlock[t].StartTime != LastRenderTime[t] || LastRenderTime[t] == TimeSpan.MinValue)
                        && (IsPlaying == false || currentBlock[t].Contains(wallClock)))
                    {
                        LastRenderTime[t] = currentBlock[t].StartTime;
                        SendBlockToRenderer(currentBlock[t], wallClock);
                        renderedBlockCount += 1;
                    }
                }

                #endregion

                #region 6. Finalize the Rendering Cycle

                // Signal the rendering cycle was set.
                BlockRenderingCycle.Set();

                // Call the update method
                foreach (var t in all)
                    Renderers[t]?.Update(wallClock);

                // Simply exit the thread when cancellation has been requested
                if (IsTaskCancellationPending) break;

                if (IsSeeking) continue;
                UpdatePosition(wallClock);

                // Spin the thread for a bit if we have no more stuff to process
                if (renderedBlockCount <= 0 && Commands.PendingCount <= 0)
                    await Task.Delay(1);

                #endregion
            }

            BlockRenderingCycle.Set();

        }

        #endregion

    }
}
