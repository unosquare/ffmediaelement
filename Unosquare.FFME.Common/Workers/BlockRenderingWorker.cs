namespace Unosquare.FFME.Workers
{
    using Commands;
    using Decoding;
    using Primitives;
    using Shared;
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;

    /// <summary>
    /// Implements the block rendering worker.
    /// </summary>
    /// <seealso cref="WorkerBase" />
    /// <seealso cref="IMediaWorker" />
    internal sealed class BlockRenderingWorker : TimerWorkerBase, IMediaWorker, ILoggingSource
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
            Container = MediaCore.Container;
            State = MediaCore.State;
        }

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => MediaCore;

        /// <summary>
        /// Gets the Media Engine's commands.
        /// </summary>
        private CommandManager Commands { get; }

        /// <summary>
        /// Gets the Media Engine's container.
        /// </summary>
        private MediaContainer Container { get; }

        /// <summary>
        /// Gets the Media Engine's state.
        /// </summary>
        private MediaEngineState State { get; }

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            // Update Status Properties
            var main = Container.Components.MainMediaType;
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
                // TODO: wait for active seek command
                try { Commands.WaitForSeekBlocks(ct); }
                catch { return; }

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
                        SendBlockToRenderer(currentBlock[t], wallClock);
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
                    if (State.HasMediaEnded == false)
                    {
                        MediaCore.Clock.Pause();
                        var endPosition = MediaCore.ChangePosition(MediaCore.Blocks[main].RangeEndTime);
                        State.UpdateMediaEnded(true, endPosition);
                        State.UpdateMediaState(PlaybackStatus.Stop);
                        foreach (var mt in Container.Components.MediaTypes)
                            MediaCore.InvalidateRenderer(mt);
                    }
                }
                else
                {
                    State.UpdateMediaEnded(false, TimeSpan.Zero);
                }

                // Update the Position
                if (!ct.IsCancellationRequested)
                    State.UpdatePosition();
            }

            #endregion
        }

        /// <inheritdoc />
        protected override void OnCycleException(Exception ex) =>
            this.LogError(Aspects.RenderingWorker, "Worker Cycle exception thrown", ex);

        /// <inheritdoc />
        protected override void OnDisposing()
        {
            // nothing needed when disposing
        }

        /// <summary>
        /// Sends the given block to its corresponding media renderer.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <returns>
        /// The number of blocks sent to the renderer
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SendBlockToRenderer(MediaBlock block, TimeSpan clockPosition)
        {
            // No blocks were rendered
            if (block == null) return 0;

            // Process property changes coming from video blocks
            State.UpdateDynamicBlockProperties(block, MediaCore.Blocks[block.MediaType]);

            // Send the block to its corresponding renderer
            MediaCore.Renderers[block.MediaType]?.Render(block, clockPosition);
            MediaCore.LastRenderTime[block.MediaType] = block.StartTime;

            // Log the block statistics for debugging
            LogRenderBlock(block, clockPosition, block.Index);

            // At this point, we are certain that a blocl has been
            // sent to its corresponding renderer.
            return 1;
        }

        /// <summary>
        /// Logs a block rendering operation as a Trace Message
        /// if the debugger is attached.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <param name="renderIndex">Index of the render.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogRenderBlock(MediaBlock block, TimeSpan clockPosition, int renderIndex)
        {
            // Prevent logging for production use
            if (MediaEngine.Platform.IsInDebugMode == false) return;

            try
            {
                var drift = TimeSpan.FromTicks(clockPosition.Ticks - block.StartTime.Ticks);
                this.LogTrace(Aspects.RenderingWorker,
                    $"{block.MediaType.ToString().Substring(0, 1)} "
                    + $"BLK: {block.StartTime.Format()} | "
                    + $"CLK: {clockPosition.Format()} | "
                    + $"DFT: {drift.TotalMilliseconds,4:0} | "
                    + $"IX: {renderIndex,3} | "
                    + $"PQ: {Container?.Components[block.MediaType]?.BufferLength / 1024d,7:0.0}k | "
                    + $"TQ: {Container?.Components.BufferLength / 1024d,7:0.0}k");
            }
            catch
            {
                // swallow
            }
        }
    }
}
