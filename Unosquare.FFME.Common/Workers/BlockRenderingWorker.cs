namespace Unosquare.FFME.Workers
{
    using Primitives;
    using Shared;
    using System;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// Implements the block rendering worker.
    /// </summary>
    /// <seealso cref="WorkerBase" />
    /// <seealso cref="IMediaWorker" />
    internal sealed class BlockRenderingWorker : TimerWorkerBase, IMediaWorker
    {
        private readonly AtomicBoolean HasInitialized = new AtomicBoolean(false);

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockRenderingWorker"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public BlockRenderingWorker(MediaEngine mediaCore)
            : base(nameof(BlockRenderingWorker), Constants.Interval.HighPriority)
        {
            MediaCore = mediaCore;
            Commands = MediaCore.Commands;
        }

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        private CommandWorker Commands { get; }

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            // Update Status Properties
            var main = MediaCore.Container.Components.MainMediaType;
            var all = MediaCore.Renderers.Keys.ToArray();
            var currentBlock = new MediaTypeDictionary<MediaBlock>();

            if (HasInitialized == false)
            {
                // wait for main component blocks or EOF or cancellation pending
                if (MediaCore.Blocks[main].Count <= 0)
                    return;

                // Set the initial clock position
                MediaCore.ChangePosition(MediaCore.Blocks[main].RangeStartTime);

                // Wait for renderers to be ready
                foreach (var t in all)
                    MediaCore.Renderers[t]?.WaitForReadyState();

                // Mark as initialized
                HasInitialized.Value = true;
            }

            #region Run the Rendering Cycle

            try
            {
                #region 2. Handle Block Rendering

                // capture the wall clock for this cycle
                var wallClock = MediaCore.WallClock;

                // Capture the blocks to render
                foreach (var t in all)
                {
                    // Get the audio, video, or subtitle block to render
                    currentBlock[t] = t == MediaType.Subtitle && MediaCore.PreloadedSubtitles != null ?
                        MediaCore.PreloadedSubtitles[wallClock] :
                        MediaCore.Blocks[t][wallClock];
                }

                // Render each of the Media Types if it is time to do so.
                foreach (var t in all)
                {
                    // Skip rendering for nulls
                    if (currentBlock[t] == null || currentBlock[t].IsDisposed)
                        continue;

                    // Render by forced signal (TimeSpan.MinValue) or because simply it is time to do so
                    if (MediaCore.LastRenderTime[t] == TimeSpan.MinValue || currentBlock[t].StartTime != MediaCore.LastRenderTime[t])
                        MediaCore.SendBlockToRenderer(currentBlock[t], wallClock);
                }

                #endregion

                #region 3. Finalize the Rendering Cycle

                // Call the update method on all renderers so they receive what the new wall clock is.
                foreach (var t in all)
                    MediaCore.Renderers[t]?.Update(wallClock);

                #endregion
            }
            catch (Exception ex)
            {
                MediaCore.LogError(Aspects.RenderingWorker, "Error while in rendering worker cycle", ex);
                throw;
            }
            finally
            {
                // Check End of Media Scenarios
                if (MediaCore.HasDecodingEnded
                && Commands.IsSeeking == false
                && MediaCore.WallClock >= MediaCore.LastRenderTime[main]
                && MediaCore.WallClock >= MediaCore.Blocks[main].RangeEndTime)
                {
                    // Rendered all and nothing else to render
                    if (MediaCore.State.HasMediaEnded == false)
                    {
                        MediaCore.Clock.Pause();
                        var endPosition = MediaCore.ChangePosition(MediaCore.Blocks[main].RangeEndTime);
                        MediaCore.State.UpdateMediaEnded(true, endPosition);
                        MediaCore.State.UpdateMediaState(PlaybackStatus.Stop);
                        foreach (var mt in MediaCore.Container.Components.MediaTypes)
                            MediaCore.InvalidateRenderer(mt);
                    }
                }
                else
                {
                    MediaCore.State.UpdateMediaEnded(false, TimeSpan.Zero);
                }

                // Update the Position
                if (ct.IsCancellationRequested == false && MediaCore.IsSyncBuffering == false)
                    MediaCore.State.UpdatePosition();
            }

            #endregion
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
