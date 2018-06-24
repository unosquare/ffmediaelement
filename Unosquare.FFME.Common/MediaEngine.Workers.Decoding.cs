namespace Unosquare.FFME
{
    using System;
    using System.Runtime.CompilerServices;
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
            #region Worker State Setup

            // The delay provider prevents 100% core usage
            var delay = new DelayProvider();

            // State variables
            var decodedFrameCount = 0;
            var wallClock = TimeSpan.Zero;
            var rangePercent = 0d;
            var isInRange = false;
            var playAfterSeek = false;

            // Holds the main media type
            var main = Container.Components.Main.MediaType;

            // Holds the auxiliary media types
            var auxs = Container.Components.MediaTypes.Except(main);

            // State properties
            var isBuffering = false;
            var resumeClock = false;

            MediaComponent comp = null;
            MediaBlockBuffer blocks = null;

            #endregion

            #region Worker Loop

            try
            {
                while (IsTaskCancellationPending == false)
                {
                    #region 1. Setup the Decoding Cycle

                    // Determine what to do on a priority command
                    if (Commands.IsExecutingDirectCommand)
                    {
                        if (Commands.IsClosing) break;
                        if (Commands.IsChanging) Commands.WaitForDirectCommand();
                    }

                    // Signal a Seek starting operation
                    FrameDecodingCycle.Begin();

                    // Chek if we have pending seeks to notify the start of a seek operation
                    if (State.IsSeeking == false && Commands.HasQueuedSeekCommands)
                    {
                        playAfterSeek = State.IsPlaying;
                        State.IsSeeking = true;
                        SendOnSeekingStarted();
                    }

                    // Execute the following command at the beginning of the cycle
                    Commands.ExecuteNextQueuedCommand();

                    // Update state properties -- this must be after processing commanmds as
                    // a command might have changed the components
                    main = Container.Components.Main.MediaType;
                    auxs = Container.Components.MediaTypes.Except(main);

                    // Set initial state
                    wallClock = WallClock;
                    decodedFrameCount = 0;

                    // Signal a Seek ending operation
                    // TOD: Maybe this should go on the block rendering worker?
                    if (State.IsSeeking && Commands.HasQueuedSeekCommands == false)
                    {
                        // Detect a end of seek cycle and update the state to the final position
                        // as set by the seek command. This is the position we already captured
                        // as the seek command was processed already.
                        State.UpdatePosition(wallClock);

                        // Call the seek method on all renderers
                        foreach (var kvp in Renderers)
                            InvalidateRenderer(kvp.Key);

                        SendOnSeekingEnded();
                        State.IsSeeking = false;
                        if (playAfterSeek)
                        {
                            Clock.Play();
                            State.UpdateMediaState(PlaybackStatus.Play);
                        }
                        else
                        {
                            State.UpdateMediaState(PlaybackStatus.Pause);
                        }
                    }
                    else if (State.IsSeeking == false)
                    {
                        // Notify position changes continuously on the state object
                        State.UpdatePosition(wallClock);
                    }

                    #endregion

                    #region 2. Main Component Decoding

                    // Capture component and blocks for easier readability
                    // comp is current component, blocks is the block collection for the component
                    comp = Container.Components[main];
                    blocks = Blocks[main];

                    // Handle the main component decoding; Start by checking we have some packets
                    WaitForPackets(comp);

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
                                WaitForPackets(comp, 1);

                                // Decode some frames and check if we are in reange now
                                decodedFrameCount += AddBlocks(main);
                                isInRange = blocks.IsInRange(wallClock);

                                // Break the cycle if we are in range
                                if (isInRange || CanReadMorePackets == false || ShouldReadMorePackets == false)
                                    break;
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

                                // Force renderer invalidation
                                InvalidateRenderer(main);

                                // Try to recover the regular loop
                                isInRange = true;
                                WaitForPackets(comp);
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

                        // wait for component to get there if we only have furutre blocks
                        // in auxiliary component.
                        if (blocks.Count > 0 && blocks.RangeStartTime > wallClock)
                            continue;

                        // We need the other components to catch up with the main
                        while (blocks.Count == 0 || blocks.RangeEndTime <= wallClock
                            || (t == MediaType.Audio && Blocks[main].Count > 0 && blocks.RangeEndTime < Blocks[main].RangeEndTime))
                        {
                            // Wait for packets if we don't have enough
                            WaitForPackets(comp, t == MediaType.Audio ? -1 : 1);

                            if (comp.PacketBufferCount <= 0)
                                break; // give up because we never received packets for the expected component
                            else
                                decodedFrameCount += AddBlocks(t);
                        }

                        // Check if we are finally within range
                        isInRange = blocks.IsInRange(wallClock);

                        // Invalidate the renderer if we don't have the block.
                        if (isInRange == false)
                            InvalidateRenderer(t);

                        // Move to the next component if we don't meet a regular conditions
                        if (isInRange == false || isBuffering || comp.PacketBufferCount <= 0)
                            continue;

                        // Decode as much as we can off the packet buffer for this cycle.
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
                            State.UpdateMediaState(PlaybackStatus.Stop, wallClock);
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
                        // Always reset the buffering flag
                        isBuffering = false;

                        // Resume the clock if it was playing
                        if (resumeClock) Clock.Play();

                        // log some message
                        Log(MediaLogMessageType.Debug, $"SYNC-BUFFER: Finished. Clock set to {wallClock.Format()}");
                    }

                    // If not already set, guess the 1-second buffer length
                    State.GuessBufferingProperties();

                    // After a seek operation, always reset the has seeked flag.
                    HasDecoderSeeked = false;

                    // Complete the frame decoding cycle
                    FrameDecodingCycle.Complete();

                    // Give it a break if there was nothing to decode.
                    // We probably need to wait for some more input
                    if (IsTaskCancellationPending == false
                        && decodedFrameCount <= 0
                        && Commands.HasQueuedCommands == false)
                    {
                        delay.WaitOne();
                    }

                    #endregion
                }
            }
            catch { throw; }
            finally
            {
                // Always exit notifying the cycle is done.
                FrameDecodingCycle.Complete();
                delay.Dispose();
            }

            #endregion
        }

        /// <summary>
        /// Invalidates the last render time for the given component.
        /// Additionally, it calls Seek on the renderer to remove any caches
        /// </summary>
        /// <param name="t">The t.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InvalidateRenderer(MediaType t)
        {
            if (State.HasMediaEnded)
                return;

            // This forces the rendering worker to send the
            // corresponding block to its renderer
            LastRenderTime[t] = TimeSpan.MinValue;
            Renderers[t]?.Seek();
        }

        /// <summary>
        /// Waits for at least 1 packet on the given media component.
        /// </summary>
        /// <param name="mediaComponent">The component.</param>
        /// <param name="cycleCount">The maximum cycles.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WaitForPackets(MediaComponent mediaComponent, int cycleCount = -1)
        {
            var cycleIndex = 0;
            while (mediaComponent.PacketBufferCount <= 0
                && CanReadMorePackets
                && ShouldReadMorePackets)
            {
                PacketReadingCycle.Wait(Constants.Interval.LowPriority);
                if (cycleCount <= 0)
                    continue;

                cycleIndex++;
                if (cycleCount >= cycleIndex)
                    break;
            }
        }
    }
}
