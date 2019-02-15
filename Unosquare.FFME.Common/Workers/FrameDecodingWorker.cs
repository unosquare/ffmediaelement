namespace Unosquare.FFME.Workers
{
    using Primitives;
    using Shared;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implement frame decoding worker logic
    /// </summary>
    /// <seealso cref="WorkerBase" />
    /// <seealso cref="IMediaWorker" />
    internal sealed class FrameDecodingWorker : ThreadWorkerBase, IMediaWorker
    {
        private bool wasSyncBuffering = false;
        private bool resumeSyncBufferingClock = false;
        private int decodedFrameCount = 0;

        public FrameDecodingWorker(MediaEngine mediaCore)
            : base(nameof(FrameDecodingWorker), ThreadPriority.Normal, Constants.Interval.HighPriority, WorkerDelayProvider.Token)
        {
            MediaCore = mediaCore;
            Commands = mediaCore.Commands;
        }

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        private CommandWorker Commands { get; }

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            #region Setup the Decoding Cycle

            // Update state properties -- this must be after processing commands as
            // a direct command might have changed the components
            var main = MediaCore.Container.Components.MainMediaType;
            MediaBlockBuffer blocks;
            decodedFrameCount = 0;
            var rangePercent = 0d;

            #endregion

            // The 2-part logic blocks detect a sync-buffering scenario
            // and then decodes the necessary frames.
            if (MediaCore.HasDecodingEnded == false && ct.IsCancellationRequested == false)
            {
                #region Sync-Buffering

                // Capture the blocks for easier readability
                blocks = MediaCore.Blocks[main];

                // If we are not then we need to begin sync-buffering
                if (wasSyncBuffering == false && blocks.IsInRange(MediaCore.WallClock) == false)
                {
                    // Signal the start of a sync-buffering scenario
                    MediaCore.IsSyncBuffering = true;
                    wasSyncBuffering = true;
                    resumeSyncBufferingClock = MediaCore.Clock.IsRunning;
                    MediaCore.Clock.Pause();
                    MediaCore.State.UpdateMediaState(PlaybackStatus.Manual);
                    MediaCore.LogDebug(Aspects.DecodingWorker, "Decoder sync-buffering started.");
                }

                #endregion

                #region Component Decoding

                // We need to add blocks if the wall clock is over 75%
                // for each of the components so that we have some buffer.
                foreach (var t in MediaCore.Container.Components.MediaTypes)
                {
                    if (ct.IsCancellationRequested) break;

                    // Capture a reference to the blocks and the current Range Percent
                    const double rangePercentThreshold = 0.75d;
                    blocks = MediaCore.Blocks[t];
                    rangePercent = blocks.GetRangePercent(MediaCore.WallClock);

                    // Read as much as we can for this cycle but always within range.
                    while (blocks.IsFull == false || rangePercent > rangePercentThreshold)
                    {
                        // Stop decoding under sync-buffering conditions
                        if (MediaCore.IsSyncBuffering && blocks.IsFull)
                            break;

                        if (ct.IsCancellationRequested || MediaCore.AddNextBlock(t) == false)
                            break;

                        decodedFrameCount += 1;
                        rangePercent = blocks.GetRangePercent(MediaCore.WallClock);

                        // Determine break conditions to save CPU time
                        if (MediaCore.IsSyncBuffering == false &&
                            rangePercent > 0 &&
                            rangePercent <= rangePercentThreshold &&
                            blocks.IsFull == false &&
                            blocks.CapacityPercent >= 0.25d &&
                            blocks.IsInRange(MediaCore.WallClock))
                            break;
                    }
                }

                // Give it a break if we are still buffering packets
                if (MediaCore.IsSyncBuffering)
                    return;

                #endregion
            }

            #region Finish the Cycle

            // Resume sync-buffering clock
            if (wasSyncBuffering && MediaCore.IsSyncBuffering == false)
            {
                // Sync-buffering blocks
                blocks = MediaCore.Blocks[main];

                // Unfortunately at this point we will need to adjust the clock after creating the frames.
                // to ensure tha main component is within the clock range if the decoded
                // frames are not with range. This is normal while buffering though.
                if (blocks.IsInRange(MediaCore.WallClock) == false)
                {
                    // Update the wall clock to the most appropriate available block.
                    if (blocks.Count > 0)
                        MediaCore.ChangePosition(blocks[MediaCore.WallClock].StartTime);
                    else
                        resumeSyncBufferingClock = false; // Hard stop the clock.
                }

                // log some message and resume the clock if it was playing
                MediaCore.LogDebug(Aspects.DecodingWorker,
                    $"Decoder sync-buffering finished. Clock set to {MediaCore.WallClock.Format()}");

                if (resumeSyncBufferingClock && MediaCore.State.HasMediaEnded == false)
                    MediaCore.ResumePlayback();

                wasSyncBuffering = false;
                resumeSyncBufferingClock = false;
            }

            // Provide updates to decoding stats
            MediaCore.State.UpdateDecodingBitRate(
                MediaCore.Blocks.Values.Sum(b => b.IsInRange(MediaCore.WallClock) ? b.RangeBitRate : 0));

            // Detect End of Decoding Scenarios
            // The Rendering will check for end of media when this
            // condition is set.
            MediaCore.HasDecodingEnded = MediaCore.DetectHasDecodingEnded(decodedFrameCount, main);

            #endregion
        }

        protected override void ExecuteCycleDelay(int wantedDelay, Task delayTask, CancellationToken token)
        {
            // We don't delay if there was output or there is a command
            // or a stop operation pending
            if (decodedFrameCount > 0)
                return;

            // Introduce a delay if the conditions above were not satisfied
            base.ExecuteCycleDelay(wantedDelay, delayTask, token);
        }

        /// <inheritdoc />
        protected override void OnCycleException(Exception ex)
        {
            // TODO: Implement
        }

        /// <inheritdoc />
        protected override void OnDisposing()
        {
            // TODO: Dispose the rednerers here
        }
    }
}
