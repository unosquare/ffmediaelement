namespace Unosquare.FFME
{
    using System;
    using System.Runtime.CompilerServices;
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
            // State variables
            var delay = new DelayProvider(); // The delay provider prevents 100% core usage
            var decodedFrameCount = 0;
            var rangePercent = 0d;
            var main = Container.Components.MainMediaType; // Holds the main media type
            var resumeClock = false;
            MediaBlockBuffer blocks = null;

            try
            {
                while (Commands.IsStopWorkersPending == false)
                {
                    #region Setup the Decoding Cycle

                    // Determine what to do on a priority command
                    if (Commands.IsExecutingDirectCommand)
                    {
                        if (Commands.IsClosing) break;
                        if (Commands.IsChanging) Commands.WaitForDirectCommand();
                    }

                    // Update state properties -- this must be after processing commanmds as
                    // a direct command might have changed the components
                    main = Container.Components.Main.MediaType;

                    // Execute the following command at the beginning of the cycle
                    Commands.ExecuteNextQueuedCommand();

                    // Signal a Seek starting operation and set the initial state
                    FrameDecodingCycle.Begin();
                    decodedFrameCount = 0;

                    #endregion

                    // The 2-part logic blocks detect a sync-buffering scenario
                    // and then decodes the necessary frames.
                    if (State.HasMediaEnded == false && IsWorkerInterruptRequested == false)
                    {
                        #region Sync-Buffering

                        // Capture the blocks for easier readability
                        blocks = Blocks[main];

                        // If we are not then we need to begin sync-buffering
                        if (blocks.IsInRange(WallClock) == false)
                        {
                            // Signal the start of a sync-buffering scenario
                            // TODO: Maybe this should be in the command manager?
                            resumeClock = Clock.IsRunning;
                            Clock.Pause();

                            Log(MediaLogMessageType.Debug, $"SYNC-BUFFER: Started.");

                            var hasAddedBlock = false;
                            while (IsWorkerInterruptRequested == false && CanReadMoreFramesOf(main))
                            {
                                if (decodedFrameCount < blocks.Capacity)
                                {
                                    hasAddedBlock = AddNextBlock(main);
                                    decodedFrameCount += hasAddedBlock ? 1 : 0;
                                }

                                if (State.IsNetworkStream &&
                                    State.IsLiveStream == false &&
                                    State.BufferingProgress < 1)
                                {
                                    PacketReadingCycle.Wait(Constants.Interval.LowPriority);
                                    continue;
                                }

                                if (decodedFrameCount > 0 && blocks.IsInRange(WallClock))
                                    break;

                                if (decodedFrameCount >= blocks.Capacity)
                                    break;

                                if (hasAddedBlock == false && CanWorkerReadPackets)
                                    PacketReadingCycle.Wait(Constants.Interval.LowPriority);
                            }

                            // Unfortunately at this point we will need to adjust the clock after creating the frames.
                            // to ensure tha mian component is within the clock range if the decoded
                            // frames are not with range. This is normal while buffering though.
                            if (blocks.IsInRange(WallClock) == false)
                            {
                                // Update the wall clock to the most appropriate available block.
                                if (blocks.Count > 0)
                                    ChangePosition(blocks[WallClock].StartTime);
                                else
                                    resumeClock = false; // Hard stop the clock.
                            }

                            // log some message and resume the clock if it was playing
                            Log(MediaLogMessageType.Debug, $"SYNC-BUFFER: Finished. Clock set to {WallClock.Format()}");
                        }

                        #endregion

                        #region Component Decoding

                        // We need to add blocks if the wall clock is over 75%
                        // for each of the components so that we have some buffer.
                        foreach (var t in Container.Components.MediaTypes)
                        {
                            if (IsWorkerInterruptRequested) break;

                            // Capture a reference to the blocks and the current Range Percent
                            blocks = Blocks[t];
                            rangePercent = blocks.GetRangePercent(WallClock);

                            // Read as much as we can for this cycle but always within range.
                            while (blocks.IsFull == false || rangePercent > 0.75d)
                            {
                                if (IsWorkerInterruptRequested || AddNextBlock(t) == false)
                                    break;

                                decodedFrameCount += 1;
                                rangePercent = blocks.GetRangePercent(WallClock);
                            }

                            if (blocks.IsInRange(WallClock) == false)
                                InvalidateRenderer(t);
                        }

                        State.UpdateDecodingBitrate();

                        #endregion
                    }

                    #region Finish the Cycle

                    // Detect End of Media Scenarios
                    DetectEndOfMedia(decodedFrameCount, main);

                    // Resume sync-buffering clock
                    if (resumeClock)
                    {
                        resumeClock = false;

                        if (State.HasMediaEnded == false)
                            Clock.Play();
                    }

                    // Complete the frame decoding cycle
                    FrameDecodingCycle.Complete();

                    // Give it a break if there was nothing to decode.
                    DelayDecoder(delay, decodedFrameCount);

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
        /// Delays the decoder loop preventing 100% CPU core usage.
        /// </summary>
        /// <param name="delay">The delay.</param>
        /// <param name="decodedFrameCount">The decoded frame count.</param>
        /// <returns>True if a delay was actually introduced</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool DelayDecoder(DelayProvider delay, int decodedFrameCount)
        {
            // We don't delay if there was output or there is a command
            // or a stop operation pending
            if (decodedFrameCount > 0 || Commands.IsStopWorkersPending || Commands.HasQueuedCommands)
                return false;

            // Introduce a delay if the conditions above were not satisfied
            delay.WaitOne();
            return true;
        }

        /// <summary>
        /// Detects the end of media.
        /// </summary>
        /// <param name="decodedFrameCount">The decoded frame count.</param>
        /// <param name="main">The main.</param>
        /// <returns>True if media ended</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool DetectEndOfMedia(int decodedFrameCount, MediaType main)
        {
            // Detect end of block rendering
            // TODO: Maybe this detection should be performed on the BlockRendering worker?
            if (decodedFrameCount <= 0
                && IsWorkerInterruptRequested == false
                && CanReadMoreFramesOf(main) == false
                && Blocks[main].IndexOf(WallClock) >= Blocks[main].Count - 1)
            {
                if (State.HasMediaEnded == false)
                {
                    // Rendered all and nothing else to read
                    Clock.Pause();
                    ChangePosition(Blocks[main].RangeEndTime);

                    if (State.NaturalDuration != null &&
                        State.NaturalDuration != TimeSpan.MinValue &&
                        State.NaturalDuration < WallClock)
                    {
                        Log(MediaLogMessageType.Warning,
                            $"{nameof(State.HasMediaEnded)} conditions met at {WallClock.Format()} but " +
                            $"{nameof(State.NaturalDuration)} reports {State.NaturalDuration.Value.Format()}");
                    }

                    State.UpdateMediaEnded(true);
                    State.UpdateMediaState(PlaybackStatus.Stop);
                    SendOnMediaEnded();
                }
            }
            else
            {
                State.UpdateMediaEnded(false);
            }

            return State.HasMediaEnded;
        }
    }
}
