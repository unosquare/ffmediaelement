namespace Unosquare.FFME
{
    using Commands;
    using Core;
    using Decoding;
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class MediaEngine
    {
        /// <summary>
        /// This partial class implements: 
        /// 1. Packet reading from the Container
        /// 2. Frame Decoding from packet buffer and Block buffering
        /// 3. Block Rendering from block buffer
        /// </summary>

        #region State Management
#pragma warning disable SA1401 // Fields must be private

        // TODO: Make this configurable
        internal static readonly Dictionary<MediaType, int> MaxBlocks = new Dictionary<MediaType, int>
        {
            { MediaType.Video, 12 },
            { MediaType.Audio, 120 },
            { MediaType.Subtitle, 120 }
        };
#pragma warning restore SA1401 // Fields must be private

        private readonly ManualResetEvent m_PacketReadingCycle = new ManualResetEvent(false);
        private readonly ManualResetEvent m_FrameDecodingCycle = new ManualResetEvent(false);
        private readonly ManualResetEvent m_BlockRenderingCycle = new ManualResetEvent(false);
        private readonly ManualResetEvent m_SeekingDone = new ManualResetEvent(true);

        private Thread PacketReadingTask = null;
        private Thread FrameDecodingTask = null;

        private AtomicBoolean m_IsTaskCancellationPending = new AtomicBoolean(false);
        private AtomicBoolean m_HasDecoderSeeked = new AtomicBoolean(false);

        /// <summary>
        /// Holds the blocks
        /// </summary>
        public MediaTypeDictionary<MediaBlockBuffer> Blocks { get; } = new MediaTypeDictionary<MediaBlockBuffer>();

        /// <summary>
        /// Gets the packet reading cycle control evenet.
        /// </summary>
        internal ManualResetEvent PacketReadingCycle => m_PacketReadingCycle;

        /// <summary>
        /// Gets the frame decoding cycle control event.
        /// </summary>
        internal ManualResetEvent FrameDecodingCycle => m_FrameDecodingCycle;

        /// <summary>
        /// Gets the block rendering cycle control event.
        /// </summary>
        internal ManualResetEvent BlockRenderingCycle => m_BlockRenderingCycle;

        /// <summary>
        /// Gets the seeking done control event.
        /// </summary>
        internal ManualResetEvent SeekingDone => m_SeekingDone;

        /// <summary>
        /// Gets or sets a value indicating whether the workedrs have been requested
        /// an exit.
        /// </summary>
        internal bool IsTaskCancellationPending
        {
            get => m_IsTaskCancellationPending.Value;
            set => m_IsTaskCancellationPending.Value = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the decoder has moved its byte position
        /// to something other than the normal continuous reads in the last read cycle.
        /// </summary>
        internal bool HasDecoderSeeked
        {
            get => m_HasDecoderSeeked.Value;
            set => m_HasDecoderSeeked.Value = value;
        }

        /// <summary>
        /// Holds the block renderers
        /// </summary>
        internal MediaTypeDictionary<IMediaRenderer> Renderers { get; } = new MediaTypeDictionary<IMediaRenderer>();

        /// <summary>
        /// Holds the last rendered StartTime for each of the media block types
        /// </summary>
        internal MediaTypeDictionary<TimeSpan> LastRenderTime { get; } = new MediaTypeDictionary<TimeSpan>();

        /// <summary>
        /// Gets a value indicating whether more packets can be read from the stream.
        /// This does not check if the packet queue is full.
        /// </summary>
        internal bool CanReadMorePackets => (Container?.IsReadAborted ?? true) == false
            && (Container?.IsAtEndOfStream ?? true) == false;

        /// <summary>
        /// Gets a value indicating whether room is available in the download cache.
        /// </summary>
        internal bool ShouldReadMorePackets
        {
            get
            {
                if (Container == null || Container.Components == null)
                    return false;

                // If it's a live stream and we have not guessed the buffer bytes per second
                // always continue reading regardless
                if (IsLiveStream && GuessedBytesRate == null)
                    return true;

                return Container.Components.PacketBufferLength < DownloadCacheLength;
            }
        }

        /// <summary>
        /// Gets a value indicating whether more frames can be decoded from the packet queue.
        /// That is, if we have packets in the packet buffer or if we are not at the end of the stream.
        /// </summary>
        internal bool CanReadMoreFrames => CanReadMorePackets || Container.Components.PacketBufferLength > 0;

        #endregion

        #region Packet Reading Worker

        /// <summary>
        /// Runs the read task which keeps a packet buffer as full as possible.
        /// It reports on DownloadProgress by enqueueing an update to the property
        /// in order to avoid any kind of disruption to this thread caused by the UI thread.
        /// </summary>
        internal void RunPacketReadingWorker()
        {
            // Holds the packet count for each read cycle
            var packetsRead = new MediaTypeDictionary<int>();
            
            // State variables for media types
            var t = MediaType.None;

            // Store Container in local variable to prevent NullReferenceException
            // when dispose occurs sametime with read cycle
            var mediaContainer = Container;

            var main = mediaContainer.Components.Main.MediaType;
            var auxs = mediaContainer.Components.MediaTypes.FundamentalAuxsFor(main);
            var all = main.JoinMediaTypes(auxs);

            // State variables for bytes read (give-up condition)
            var startBytesRead = 0UL;
            var currentBytesRead = 0UL;

            try
            {
                // Worker logic begins here
                while (IsTaskCancellationPending == false)
                {
                    // Wait for seeking to be done.
                    SeekingDone.WaitOne();

                    // Enter a packet reading cycle
                    PacketReadingCycle.Reset();

                    // Initialize Packets read to 0 for each component and state variables
                    foreach (var k in mediaContainer.Components.MediaTypes)
                        packetsRead[k] = 0;

                    startBytesRead = mediaContainer.Components.TotalBytesRead;
                    currentBytesRead = 0UL;

                    // Start to perform the read loop
                    while (CanReadMorePackets && ShouldReadMorePackets && IsTaskCancellationPending == false)
                    {
                        // Perform a packet read. t will hold the packet type.
                        t = mediaContainer.Read();

                        // Discard packets that we don't need (i.e. MediaType == None)
                        if (mediaContainer.Components.MediaTypes.HasMediaType(t) == false)
                            continue;

                        // Update the packet count for the components
                        packetsRead[t] += 1;

                        // The give-up condition is that in spite of efforts to read at least one of each,
                        // we could not find the required packet types.
                        currentBytesRead = mediaContainer.Components.TotalBytesRead - startBytesRead;
                        if (currentBytesRead > (ulong)DownloadCacheLength)
                            break;

                        // Ensure we have read at least some packets from main and auxiliary streams.
                        if (packetsRead.AllGreaterThanZero())
                            break;
                    }

                    // finish the reading cycle.
                    PacketReadingCycle.Set();

                    // Wait some if we have a full packet buffer or we are unable to read more packets (i.e. EOF).
                    if (ShouldReadMorePackets == false
                        || CanReadMorePackets == false
                        || currentBytesRead <= 0)
                    {
                        Task.Delay(1).GetAwaiter().GetResult();
                    }
                }
            }
            catch (ThreadAbortException)
            {
            }
            catch
            {
                throw;
            }
            finally
            {
                // Always exit notifying the reading cycle is done.
                PacketReadingCycle.Set();
            }
        }

        #endregion

        #region Frame Decoding Worker

        /// <summary>
        /// Continually decodes the available packet buffer to have as
        /// many frames as possible in each frame queue and
        /// up to the MaxFrames on each component
        /// </summary>
        internal void RunFrameDecodingWorker()
        {
            try
            {
                // State variables
                var decodedFrameCount = 0;
                var wallClock = TimeSpan.Zero;
                var rangePercent = 0d;
                var isInRange = false;

                // Holds the main media type
                var main = Container.Components.Main.MediaType;

                // Holds the auxiliary media types
                var auxs = Container.Components.MediaTypes.ExcludeMediaType(main);

                // Holds all components
                var all = Container.Components.MediaTypes.DeepCopy();

                var isBuffering = false;
                var resumeClock = false;
                var hasPendingSeeks = false;

                MediaComponent comp = null;
                MediaBlockBuffer blocks = null;

                while (IsTaskCancellationPending == false)
                {
                    #region 1. Setup the Decoding Cycle

                    // Singal a Seek starting operation
                    hasPendingSeeks = Commands.PendingCountOf(MediaCommandType.Seek) > 0;
                    if (IsSeeking == false && hasPendingSeeks)
                    {
                        IsSeeking = true;
                        SendOnSeekingStarted();
                    }

                    // Execute the following command at the beginning of the cycle
                    Commands.ProcessNext();

                    // Signal a Seek ending operation
                    hasPendingSeeks = Commands.PendingCountOf(MediaCommandType.Seek) > 0;
                    if (IsSeeking == true && hasPendingSeeks == false)
                    {
                        SnapVideoPosition(Clock.Position);
                        IsSeeking = false;

                        // Call the seek method on all renderers
                        foreach (var kvp in Renderers)
                            kvp.Value.Seek();

                        SendOnSeekingEnded();
                    }

                    // Wait for a seek operation to complete (if any)
                    // and initiate a frame decoding cycle.
                    SeekingDone.WaitOne();

                    // Initiate the frame docding cycle
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
                    while (comp.PacketBufferCount <= 0 && CanReadMorePackets)
                        PacketReadingCycle.WaitOne();

                    if (comp.PacketBufferCount > 0)
                    {
                        // Detect if we are in range for the main component
                        isInRange = blocks.IsInRange(wallClock);

                        if (isInRange == false)
                        {
                            // Signal the start of a sync-buffering scenario
                            HasDecoderSeeked = true;
                            isBuffering = true;
                            resumeClock = Clock.IsRunning;
                            Clock.Pause();
                            Log(MediaLogMessageType.Debug, $"SYNC-BUFFER: Started.");

                            // Read some frames and try to get a valid range
                            do
                            {
                                // Try to get more packets by waiting for read cycles.
                                if (CanReadMorePackets && comp.PacketBufferCount <= 0)
                                    PacketReadingCycle.WaitOne();

                                // Decode some frames and check if we are in reange now
                                decodedFrameCount += AddBlocks(main);
                                isInRange = blocks.IsInRange(wallClock);

                                // Break the cycle if we are in range
                                if (isInRange || CanReadMorePackets == false) { break; }
                            }
                            while (decodedFrameCount <= 0 && blocks.IsFull == false);

                            // Unfortunately at this point we will need to adjust the clock after creating the frames.
                            // to ensure tha mian component is within the clock range if the decoded
                            // frames are not with range. This is normal while buffering though.
                            if (isInRange == false)
                            {
                                // Update the wall clock to the most appropriate available block.
                                if (blocks.Count > 0)
                                    wallClock = blocks[wallClock].StartTime;
                                else
                                    resumeClock = false; // Hard stop the clock.

                                // Update the clock to what the main component range mandates
                                Clock.Position = wallClock;

                                // Call seek to invalidate renderer
                                Renderers[main].Seek();

                                // Try to recover the regular loop
                                isInRange = true;
                                while (CanReadMorePackets && comp.PacketBufferCount <= 0)
                                    PacketReadingCycle.WaitOne();
                            }
                        }

                        if (isInRange)
                        {
                            // Check if we need more blocks for the current components
                            rangePercent = blocks.GetRangePercent(wallClock);

                            // Read as much as we can for this cycle.
                            while (comp.PacketBufferCount > 0)
                            {
                                rangePercent = blocks.GetRangePercent(wallClock);

                                if (blocks.IsFull == false || (blocks.IsFull && rangePercent > 0.75d && rangePercent < 1d))
                                    decodedFrameCount += AddBlocks(main);
                                else
                                    break;
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
                        isInRange = blocks.IsInRange(wallClock);

                        // Invalidate the renderer if we don't have the block.
                        if (isInRange == false) Renderers[t].Seek();

                        // wait for component to get there if we only have furutre blocks
                        // in auxiliary component.
                        if (blocks.Count > 0 && blocks.RangeStartTime > wallClock)
                            continue;

                        // Try to catch up with the wall clock
                        while (blocks.Count == 0 || blocks.RangeEndTime <= wallClock)
                        {
                            // Wait for packets if we don't have enough packets
                            if (CanReadMorePackets && comp.PacketBufferCount <= 0)
                                PacketReadingCycle.WaitOne();

                            if (comp.PacketBufferCount <= 0)
                                break;
                            else
                                decodedFrameCount += AddBlocks(t);
                        }

                        isInRange = blocks.IsInRange(wallClock);

                        // Move to the next component if we don't meet a regular conditions
                        if (isInRange == false || isBuffering || comp.PacketBufferCount <= 0)
                            continue;

                        // Read as much as we can for this cycle.
                        while (comp.PacketBufferCount > 0)
                        {
                            rangePercent = blocks.GetRangePercent(wallClock);

                            if (blocks.IsFull == false || (blocks.IsFull && rangePercent > 0.75d && rangePercent < 1d))
                                decodedFrameCount += AddBlocks(t);
                            else
                                break;
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
                            Clock.Position = NaturalDuration ?? Blocks[main].RangeEndTime;
                            wallClock = Clock.Position;

                            HasMediaEnded = true;
                            MediaState = MediaEngineState.Pause;
                            SendOnMediaEnded();
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
                        // Reset the buffering flag
                        isBuffering = false;

                        // Resume the clock if it was playing
                        if (resumeClock) Clock.Play();

                        // log some message
                        Log(
                            MediaLogMessageType.Debug,
                            $"SYNC-BUFFER: Finished. Clock set to {wallClock.Format()}");
                    }

                    // Complete the frame decoding cycle
                    FrameDecodingCycle.Set();

                    // After a seek operation, always reset the has seeked flag.
                    HasDecoderSeeked = false;

                    // If not already set, guess the 1-second buffer length
                    GuessBufferingProperties();

                    // Give it a break if there was nothing to decode.
                    // We probably need to wait for some more input
                    if (decodedFrameCount <= 0 && Commands.PendingCount <= 0)
                        Task.Delay(1).GetAwaiter().GetResult();

                    #endregion
                }
            }
            catch (ThreadAbortException)
            {
            }
            catch
            {
                throw;
            }
            finally
            {
                // Always exit notifying the cycle is done.
                FrameDecodingCycle.Set();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the media block buffers and 
        /// starts packet reader, frame decoder, and block rendering workers.
        /// </summary>
        internal void StartWorkers()
        {
            // Initialize the block buffers
            foreach (var t in Container.Components.MediaTypes)
            {
                Blocks[t] = new MediaBlockBuffer(MaxBlocks[t], t);
                LastRenderTime[t] = TimeSpan.MinValue;
                Renderers[t] = Platform.CreateRenderer(t, this);
            }

            Clock.SpeedRatio = Defaults.DefaultSpeedRatio;
            IsTaskCancellationPending = false;

            // Set the initial state of the task cycles.
            SeekingDone.Set();
            BlockRenderingCycle.Reset();
            FrameDecodingCycle.Reset();
            PacketReadingCycle.Reset();

            // Create the thread runners
            PacketReadingTask = new Thread(RunPacketReadingWorker)
            { IsBackground = true, Name = nameof(PacketReadingTask), Priority = ThreadPriority.Normal };

            FrameDecodingTask = new Thread(RunFrameDecodingWorker)
            { IsBackground = true, Name = nameof(FrameDecodingTask), Priority = ThreadPriority.AboveNormal };

            // Fire up the threads
            PacketReadingTask.Start();
            FrameDecodingTask.Start();
            StartBlockRenderingWorker(); // BlockRenderingTask.Start();
        }

        /// <summary>
        /// Stops the packet reader, frame decoder, and block renderers
        /// </summary>
        internal void StopWorkers()
        {
            StopBlockRenderingWorker();

            // Wait for worker threads to finish
            var wrokers = new[] { PacketReadingTask, FrameDecodingTask };
            foreach (var w in wrokers)
            {
                // Abort causes memory leaks bacause packets and frames might not get disposed by the corresponding workers.
                // w.Abort();

                // Wait for all threads to join
                w.Join();
            }

            // Set the threads to null
            FrameDecodingTask = null;
            PacketReadingTask = null;
        }

        /// <summary>
        /// Sets the clock to a discrete video position if possible
        /// </summary>
        /// <param name="position">The position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SnapVideoPosition(TimeSpan position)
        {
            if (Container == null) return;

            // Set the clock to a discrete video position if possible
            if (Container.Components.Main.MediaType == MediaType.Video
                && Blocks[MediaType.Video].IsInRange(position))
            {
                var block = Blocks[MediaType.Video][position];
                if (block != null && block.Duration.Ticks > 0 && VideoFrameRate != 0d)
                    Clock.Position = block.SnapTime;
            }
        }

        /// <summary>
        /// Gets a value indicating whether more frames can be converted into blocks of the given type.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>
        ///   <c>true</c> if this instance [can read more frames of] the specified t; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanReadMoreFramesOf(MediaType t)
        {
            return CanReadMorePackets || Container.Components[t].PacketBufferLength > 0;
        }

        /// <summary>
        /// Sends the given block to its corresponding media renderer.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <returns>The number of blocks sent to the renderer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SendBlockToRenderer(MediaBlock block, TimeSpan clockPosition)
        {
            // Process property changes coming from video blocks
            if (block != null && block.MediaType == MediaType.Video)
            {
                if (block is VideoBlock videoBlock)
                {
                    VideoSmtpeTimecode = videoBlock.SmtpeTimecode;
                    VideoHardwareDecoder = (Container?.Components?.Video?.IsUsingHardwareDecoding ?? false) ?
                        Container?.Components?.Video?.HardwareAccelerator?.Name ?? string.Empty : string.Empty;
                }
            }

            // Send the block to its corresponding renderer
            Renderers[block.MediaType]?.Render(block, clockPosition);

            // Extension method for logging
            this.LogRenderBlock(block, clockPosition, Blocks[block.MediaType].IndexOf(clockPosition));
            LastRenderTime[block.MediaType] = block.StartTime;
            return 1;
        }

        /// <summary>
        /// Adds the blocks of the given media type.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>The number of blocks that were added</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    }
}
