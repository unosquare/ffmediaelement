namespace Unosquare.FFME
{
    using Primitives;
    using Shared;
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;

    public partial class MediaEngine
    {
        /// <summary>
        /// Continually decodes the available packet buffer to have as
        /// many frames as possible in each frame queue and
        /// up to the MaxFrames on each component
        /// </summary>
        internal void RunFrameDecodingWorker()
        {
            // TODO: Don't use State properties in workers as they are only for
            // TODO: Check the use of wall clock. Maybe it's be more consistent
            // to use a single atomic wall clock value per cycle. Check other workers as well.
            // state notification purposes.
            // State variables
            var wasSyncBuffering = false;
            var delay = new DelayProvider(); // The delay provider prevents 100% core usage
            int decodedFrameCount;
            double rangePercent;
            MediaType main; // Holds the main media type
            var resumeSyncBufferingClock = false;
            MediaBlockBuffer blocks;

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

                    // Execute the following command at the beginning of the cycle
                    if (IsSyncBuffering == false)
                        Commands.ExecuteNextQueuedCommand();

                    // Signal a Seek starting operation and set the initial state
                    FrameDecodingCycle.Begin();

                    // Update state properties -- this must be after processing commands as
                    // a direct command might have changed the components
                    main = Container.Components.MainMediaType;
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
                        if (wasSyncBuffering == false && blocks.IsInRange(WallClock) == false)
                        {
                            // Signal the start of a sync-buffering scenario
                            IsSyncBuffering = true;
                            wasSyncBuffering = true;
                            resumeSyncBufferingClock = Clock.IsRunning;
                            Clock.Pause();
                            State.UpdateMediaState(PlaybackStatus.Manual);
                            this.LogDebug(Aspects.DecodingWorker, "Decoder sync-buffering started.");
                        }

                        #endregion

                        #region Component Decoding

                        // We need to add blocks if the wall clock is over 75%
                        // for each of the components so that we have some buffer.
                        foreach (var t in Container.Components.MediaTypes)
                        {
                            if (IsWorkerInterruptRequested) break;

                            // Capture a reference to the blocks and the current Range Percent
                            const double rangePercentThreshold = 0.75d;
                            blocks = Blocks[t];
                            rangePercent = blocks.GetRangePercent(WallClock);

                            // Read as much as we can for this cycle but always within range.
                            while (blocks.IsFull == false || rangePercent > rangePercentThreshold)
                            {
                                // Stop decoding under sync-buffering conditions
                                if (IsSyncBuffering && blocks.IsFull)
                                    break;

                                if (IsWorkerInterruptRequested || AddNextBlock(t) == false)
                                    break;

                                decodedFrameCount += 1;
                                rangePercent = blocks.GetRangePercent(WallClock);

                                // Determine break conditions to save CPU time
                                if (IsSyncBuffering == false &&
                                    rangePercent > 0 &&
                                    rangePercent <= rangePercentThreshold &&
                                    blocks.IsFull == false &&
                                    blocks.CapacityPercent >= 0.25d &&
                                    blocks.IsInRange(WallClock))
                                    break;
                            }
                        }

                        // Give it a break if we are still buffering packets
                        if (IsSyncBuffering)
                        {
                            delay.WaitOne();
                            FrameDecodingCycle.Complete();
                            continue;
                        }

                        #endregion
                    }

                    #region Finish the Cycle

                    // Detect End of Media Scenarios
                    DetectEndOfMedia(decodedFrameCount, main);

                    // Resume sync-buffering clock
                    if (wasSyncBuffering && IsSyncBuffering == false)
                    {
                        // Sync-buffering blocks
                        blocks = Blocks[main];

                        // Unfortunately at this point we will need to adjust the clock after creating the frames.
                        // to ensure tha main component is within the clock range if the decoded
                        // frames are not with range. This is normal while buffering though.
                        if (blocks.IsInRange(WallClock) == false)
                        {
                            // Update the wall clock to the most appropriate available block.
                            if (blocks.Count > 0)
                                ChangePosition(blocks[WallClock].StartTime);
                            else
                                resumeSyncBufferingClock = false; // Hard stop the clock.
                        }

                        // log some message and resume the clock if it was playing
                        this.LogDebug(Aspects.DecodingWorker,
                            $"Decoder sync-buffering finished. Clock set to {WallClock.Format()}");

                        if (resumeSyncBufferingClock && State.HasMediaEnded == false)
                            ResumePlayback();

                        wasSyncBuffering = false;
                        resumeSyncBufferingClock = false;
                    }

                    // Provide updates to decoding stats
                    State.UpdateDecodingBitRate(
                        Blocks.Values.Sum(b => b.IsInRange(WallClock) ? b.RangeBitRate : 0));

                    // Complete the frame decoding cycle
                    FrameDecodingCycle.Complete();

                    // Give it a break if there was nothing to decode.
                    DelayDecoder(delay, decodedFrameCount);

                    #endregion
                }
            }
            finally
            {
                // Reset decoding stats
                State.UpdateDecodingBitRate(0);

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
                if (State.HasMediaEnded)
                    return State.HasMediaEnded;

                // Rendered all and nothing else to read
                Clock.Pause();
                ChangePosition(Blocks[main].RangeEndTime);

                if (State.NaturalDuration != null &&
                    State.NaturalDuration != TimeSpan.MinValue &&
                    State.NaturalDuration < WallClock)
                {
                    this.LogWarning(Aspects.DecodingWorker,
                        $"{nameof(State.HasMediaEnded)} conditions met at {WallClock.Format()} but " +
                        $"{nameof(State.NaturalDuration)} reports {State.NaturalDuration.Value.Format()}");
                }

                State.UpdateMediaEnded(true);
                State.UpdateMediaState(PlaybackStatus.Stop);
                foreach (var mt in Container.Components.MediaTypes)
                    InvalidateRenderer(mt);

                SendOnMediaEnded();
            }
            else
            {
                State.UpdateMediaEnded(false);
            }

            return State.HasMediaEnded;
        }
    }
}
