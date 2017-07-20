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

        // TODO: Make this dynamic
        internal static readonly Dictionary<MediaType, int> MaxBlocks = new Dictionary<MediaType, int>
        {
            { MediaType.Video, 12 },
            { MediaType.Audio, 128 },
            { MediaType.Subtitle, 128 }
        };

        #endregion

        #region State Variables

        internal readonly MediaTypeDictionary<MediaBlockBuffer> Blocks
            = new MediaTypeDictionary<MediaBlockBuffer>();

        internal readonly MediaTypeDictionary<IRenderer> Renderers
            = new MediaTypeDictionary<IRenderer>();

        internal readonly MediaTypeDictionary<TimeSpan> LastRenderTime
            = new MediaTypeDictionary<TimeSpan>();

        internal readonly MediaTypeDictionary<MediaBlock> CurrentBlock
            = new MediaTypeDictionary<MediaBlock>();

        internal volatile bool IsTaskCancellationPending = false;

        internal readonly ReaderWriterLock CurrentBlockLocker = new ReaderWriterLock();

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


        #endregion

        #region Methods

        /// <summary>
        /// Gets a value indicating whether more frames can be converted into blocks of the given type.
        /// </summary>
        private bool CanReadMoreFramesOf(MediaType t) { return CanReadMorePackets || Container.Components[t].PacketBufferLength > 0; }


        /// <summary>
        /// Buffers some packets which in turn get decoded into frames and then
        /// converted into blocks.
        /// </summary>
        /// <param name="packetBufferLength">Length of the packet buffer.</param>
        /// <param name="clearExisting">if set to <c>true</c> clears the existing frames and blocks.</param>
        private void BufferBlocks(int packetBufferLength, bool clearExisting)
        {
            var resumeClock = Clock.IsRunning;
            Clock.Pause();

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
            while (CanReadMoreFramesOf(main) && Blocks[main].CapacityPercent <= 0.5d)
            {
                //PacketReadingCycle.WaitOne();
                FrameDecodingCycle.WaitOne();
                BufferingProgress = Blocks[main].CapacityPercent / 0.5d;
            }

            // Raise the buffering started event.
            if (resumeClock) { Clock.Play(); }

            BufferingProgress = 1;
            IsBuffering = false;
            RaiseBufferingEndedEvent();

        }

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

                // Wait some if we have a full packet buffer or we are unable to read more packets.
                if (IsTaskCancellationPending == false
                    && (Container.Components.PacketBufferLength >= DownloadCacheLength || CanReadMorePackets == false))
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

            var t = MediaType.None;
            var wallClock = TimeSpan.Zero;
            var rangePercent = 0d;
            var isInRange = false;
            var main = Container.Components.Main.MediaType;

            while (IsTaskCancellationPending == false)
            {
                // Wait for a seek operation to complete (if any)
                // and initiate a frame decoding cycle.
                SeekingDone.WaitOne();
                FrameDecodingCycle.Reset();

                wallClock = Clock.Position;

                // Decode Frames if necessary
                decodedFrameCount = 0;

                // Decode frames for each of the components
                foreach (var component in Container.Components.All)
                {
                    // Don't do anything if we don't have packets to decode
                    if (component.PacketBufferCount <= 0)
                        continue;

                    t = component.MediaType;

                    // Clear the media blocks if we are outside of the required range
                    // we don't need them and we now need as many playback blocks as we can have available
                    if (Blocks[t].IsInRange(wallClock) == false)
                        Blocks[t].Clear();

                    // Check if we need more blocks for the current components
                    rangePercent = Blocks[t].RangeDuration.Ticks == 0 ? 0d :
                        ((double)wallClock.Ticks - Blocks[t].RangeStartTime.Ticks) / Blocks[t].RangeDuration.Ticks;

                    isInRange = Blocks[t].IsInRange(wallClock);

                    // Read as many blocks as we possibly can until the ranges match.
                    // TODO: Add a gicve-up condition somewhere in case something causes an infinite loop
                    while (component.PacketBufferCount > 0 &&
                        ((rangePercent > 0.75d && Blocks[t].IsFull) || isInRange == false || (isInRange && Blocks[t].IsFull == false)))
                    {
                        // Decode the frames
                        var frames = component.ReceiveFrames();

                        // exit the loop if there was nothing more to decode
                        if (frames.Count == 0) break;
                        foreach (var frame in frames)
                        {
                            // Add each decoded frame as a playback block
                            if (frame == null) continue;
                            Blocks[t].Add(frame, Container);
                            decodedFrameCount += 1;
                        }

                        // update the range percent.
                        rangePercent = Blocks[t].RangeDuration.Ticks == 0 ? 0d :
                            ((double)wallClock.Ticks - Blocks[t].RangeStartTime.Ticks) / Blocks[t].RangeDuration.Ticks;

                        isInRange = Blocks[t].IsInRange(wallClock);
                    }
                }

                CurrentBlockLocker.AcquireWriterLock(Timeout.Infinite);
                foreach (var component in Container.Components.All)
                {
                    t = component.MediaType;
                    CurrentBlock[t] = Blocks[t][wallClock];
                }
                CurrentBlockLocker.ReleaseWriterLock();

                #region Detect End of Media

                // Detect end of block rendering
                if (CanReadMoreFramesOf(main) == false && Blocks[main].IndexOf(wallClock) == Blocks[main].Count - 1)
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
                        UpdatePosition(Clock.Position);
                        RaiseMediaEndedEvent();
                    }
                }
                else
                {
                    HasMediaEnded = false;
                }

                #endregion

                // Complete the frame decoding cycle
                FrameDecodingCycle.Set();

                // Give it a break if there was nothing to decode.
                // We probably need to wait for some more input
                if (decodedFrameCount <= 0)
                    await Task.Delay(1);

            }

            CurrentBlockLocker.ReleaseLock();
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

            // reset render times for all components
            foreach (var t in all)
                LastRenderTime[t] = TimeSpan.MinValue;

            // Buffer some blocks and adjust the clock to the start position
            BufferBlocks(BufferCacheLength, false);
            Clock.Position = Blocks[main].RangeStartTime;
            var wallClock = Clock.Position;

            var clockDirection = ClockDirection.Forward;

            #endregion

            while (true)
            {
                #region 1. Control and Capture

                // Execute the following command at the beginning of the cycle
                await Commands.ProcessNext();

                // Check if one of the commands has requested an exit
                if (IsTaskCancellationPending) break;

                // Capture current clock position for the rest of this cycle
                BlockRenderingCycle.Reset();

                // Detect the clock's direction
                if (HasMediaEnded == false && Clock.IsRunning == false
                    && clockDirection != ClockDirection.Backward && Clock.Position.Ticks < wallClock.Ticks)
                    clockDirection = ClockDirection.Backward;
                else
                    clockDirection = ClockDirection.Forward;

                wallClock = Clock.Position;

                #endregion

                #region 2. Handle Block Rendering

                // Render each of the Media Types if it is time to do so.

                CurrentBlockLocker.AcquireReaderLock(Timeout.Infinite);

                foreach (var t in all)
                {
                    if (CurrentBlock[t] == null)
                        continue;

                    // render the frame if we have not rendered
                    if ((CurrentBlock[t].StartTime != LastRenderTime[t] || LastRenderTime[t] == TimeSpan.MinValue)
                        && (IsPlaying == false || wallClock.Ticks >= CurrentBlock[t].StartTime.Ticks))
                    {
                        LastRenderTime[t] = CurrentBlock[t].StartTime;
                        SendBlockToRenderer(CurrentBlock[t], wallClock);
                    }
                }

                CurrentBlockLocker.ReleaseReaderLock();

                #endregion

                #region 6. Finalize the Rendering Cycle

                // Calll the update method
                foreach (var t in all)
                    Renderers[t]?.Update(wallClock);

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
