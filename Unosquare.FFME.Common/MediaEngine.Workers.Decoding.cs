namespace Unosquare.FFME
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Unosquare.FFME.Commands;
    using Unosquare.FFME.Decoding;
    using Unosquare.FFME.Primitives;
    using Unosquare.FFME.Shared;

    public partial class MediaEngine
    {
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
                    if (State.IsSeeking == false && hasPendingSeeks)
                    {
                        State.IsSeeking = true;
                        SendOnSeekingStarted();
                    }

                    // Execute the following command at the beginning of the cycle
                    Commands.ProcessNext();

                    // Wait for a seek operation to complete (if any)
                    // and initiate a frame decoding cycle.
                    SeekingDone.WaitOne();

                    // Signal a Seek ending operation
                    hasPendingSeeks = Commands.PendingCountOf(MediaCommandType.Seek) > 0;
                    if (State.IsSeeking && hasPendingSeeks == false)
                    {
                        Clock.Update(SnapToFramePosition(WallClock));
                        State.IsSeeking = false;

                        // Call the seek method on all renderers
                        foreach (var kvp in Renderers)
                        {
                            LastRenderTime[kvp.Key] = TimeSpan.MinValue;
                            kvp.Value.Seek();
                        }

                        SendOnSeekingEnded();
                    }

                    // Initiate the frame docding cycle
                    FrameDecodingCycle.Reset();

                    // Set initial state
                    wallClock = WallClock;
                    decodedFrameCount = 0;

                    #endregion

                    #region 2. Main Component Decoding

                    // Capture component and blocks for easier readability
                    comp = Container.Components[main];
                    blocks = Blocks[main];

                    // Handle the main component decoding; Start by checking we have some packets
                    while (comp.PacketBufferCount <= 0 && CanReadMorePackets)
                        PacketReadingCycle.WaitOne(Constants.Interval.LowPriority);

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
                                Clock.Update(wallClock);

                                // Call seek to invalidate renderer
                                LastRenderTime[main] = TimeSpan.MinValue;
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
                        if (State.IsSeeking) continue;

                        // Capture the current block buffer and component
                        // for easier readability
                        comp = Container.Components[t];
                        blocks = Blocks[t];
                        isInRange = blocks.IsInRange(wallClock);

                        // Invalidate the renderer if we don't have the block.
                        if (isInRange == false)
                        {
                            LastRenderTime[t] = TimeSpan.MinValue;
                            Renderers[t].Seek();
                        }

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
                    // TODO: Maybe this detection should be performed on the BlockRendering worker?
                    if (isBuffering == false
                        && State.IsSeeking == false
                        && CanReadMoreFramesOf(main) == false
                        && Blocks[main].IndexOf(wallClock) == Blocks[main].Count - 1)
                    {
                        if (State.HasMediaEnded == false)
                        {
                            // Rendered all and nothing else to read
                            Clock.Pause();

                            if (State.NaturalDuration != null && State.NaturalDuration != TimeSpan.MinValue)
                                wallClock = State.NaturalDuration.Value;
                            else
                                wallClock = Blocks[main].RangeEndTime;

                            Clock.Update(wallClock);
                            State.HasMediaEnded = true;
                            State.UpdateMediaState(PlaybackStatus.Pause, wallClock);
                            SendOnMediaEnded();
                        }
                    }
                    else
                    {
                        State.HasMediaEnded = false;
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
                    State.GuessBufferingProperties();

                    // Give it a break if there was nothing to decode.
                    // We probably need to wait for some more input
                    if (decodedFrameCount <= 0 && Commands.PendingCount <= 0)
                        Task.Delay(1).GetAwaiter().GetResult();

                    #endregion
                }
            }
            catch (ThreadAbortException) { /* swallow */ }
            catch { if (!IsDisposing && !IsDisposed) throw; }
            finally
            {
                // Always exit notifying the cycle is done.
                FrameDecodingCycle.Set();
            }
        }
    }
}
