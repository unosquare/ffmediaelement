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
            : base(nameof(BlockRenderingWorker), DefaultPeriod)
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
            var playbackPosition = new MediaTypeDictionary<TimeSpan>();

            if (HasInitialized == false)
            {
                // wait for main component blocks or EOF or cancellation pending
                if (MediaCore.Blocks[main].Count <= 0)
                    return;

                // Set the initial clock position
                // MediaCore.ChangePosition(MediaCore.Blocks[main].RangeStartTime);

                // Wait for renderers to be ready
                foreach (var t in all)
                    MediaCore.Renderers[t]?.WaitForReadyState();

                // Mark as initialized
                HasInitialized.Value = true;
            }

            #region Run the Rendering Cycle

            try
            {
                #region 1. Wait for any seek operation to make blocks available in a loop

                while (!ct.IsCancellationRequested
                    && Commands.IsActivelySeeking
                    && !MediaCore.Blocks[main].IsInRange(MediaCore.WallClock))
                {
                    // Check if we finally have seek blocks available
                    // if we don't get seek blocks in range and we are not step-seeking,
                    // then we simply break out of the loop and render whatever it is we have
                    // to create the illussion of smooth seeking. For precision seeking we
                    // continue the loop.
                    if (!Commands.WaitForSeekBlocks(DefaultPeriod.Milliseconds)
                        && Commands.ActiveSeekMode == CommandManager.SeekMode.Normal)
                    {
                        break;
                    }
                }

                #endregion

                #region 2. Handle Block Rendering

                // Capture the blocks to render at a fixed wall clock position
                // so all blocks are aligned to the same timestamp
                var wallClock = MediaCore.WallClock;

                foreach (var t in all)
                {
                    // Get the timestamp for the component based on the captured wall clock
                    playbackPosition[t] = GetPlaybackPosition(t, wallClock);

                    // skip blocks if we are seeking and they are not video blocks
                    if (Commands.IsSeeking && t != MediaType.Video)
                    {
                        currentBlock[t] = null;
                        continue;
                    }

                    // Get the audio, video, or subtitle block to render
                    currentBlock[t] = t == MediaType.Subtitle && MediaCore.PreloadedSubtitles != null ?
                        MediaCore.PreloadedSubtitles[playbackPosition[t]] :
                        MediaCore.Blocks[t][playbackPosition[t]];
                }

                // Render each of the Media Types if it is time to do so.
                foreach (var t in all)
                {
                    // Don't send null blocks to renderer
                    if (currentBlock[t] == null || currentBlock[t].IsDisposed)
                        continue;

                    // Render by forced signal (TimeSpan.MinValue) or because simply it is time to do so
                    if (MediaCore.LastRenderTime[t] == TimeSpan.MinValue || currentBlock[t].StartTime != MediaCore.LastRenderTime[t])
                        SendBlockToRenderer(currentBlock[t], playbackPosition[t]);

                    // TODO: Maybe SendBlockToRenderer repeatedly for contiguous audio blocks
                    // Also remove the logic where the renderer reads the contiguous audio frames
                }

                #endregion

                #region 3. Finalize the Rendering Cycle

                // Call the update method on all renderers so they receive what the new wall clock is.
                foreach (var t in all)
                    MediaCore.Renderers[t]?.Update(playbackPosition[t]);

                #endregion
            }
            catch (Exception ex)
            {
                MediaCore.LogError(Aspects.RenderingWorker, "Error while in rendering worker cycle", ex);
                throw;
            }
            finally
            {
                var playbackEndTime = AbsoluteComponentClock(main, MediaCore.Blocks[main].RangeEndTime);

                // Check End of Media Scenarios
                if (Commands.IsSeeking == false
                && MediaCore.HasDecodingEnded
                && MediaCore.WallClock.Ticks >= playbackEndTime.Ticks)
                {
                    // Rendered all and nothing else to render
                    if (State.HasMediaEnded == false)
                    {
                        MediaCore.Clock.Pause();

                        // TODO: compute end position
                        // var endPosition = TimeSpan.FromTicks(MediaCore.LastRenderTime[main].Ticks + RenderDuration[main].Ticks);
                        // endPosition = MediaCore.ChangePosition(endPosition);
                        // State.UpdateMediaEnded(true, endPosition);
                        // TODO: The below needs adding the last block durration
                        var endPosition = MediaCore.ChangePosition(playbackEndTime);
                        State.UpdateMediaEnded(true, endPosition);

                        State.UpdateMediaState(PlaybackStatus.Stop);
                        MediaCore.InvalidateRenderers();
                    }
                }
                else
                {
                    State.UpdateMediaEnded(false, TimeSpan.Zero);
                }

                // Update the Position
                if (!ct.IsCancellationRequested && Commands.IsSeeking == false)
                    State.UpdatePosition(playbackPosition.ContainsKey(MediaType.Audio) ? playbackPosition[MediaType.Audio] : playbackPosition[main]);
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
        /// Converts from absolute wall clock time to the corresponding component-equivalent time
        /// </summary>
        /// <param name="t">The t.</param>
        /// <param name="wallClock">The wall clock.</param>
        /// <returns>The wall clock timestamp that maps to a corresponding component time</returns>
        private TimeSpan GetPlaybackPosition(MediaType t, TimeSpan wallClock)
        {
            var offset = Container.Components[t]?.StartTime ?? TimeSpan.MinValue;
            offset = offset == TimeSpan.MinValue ? TimeSpan.Zero : offset;
            return TimeSpan.FromTicks(wallClock.Ticks + offset.Ticks);
        }

        private TimeSpan AbsoluteComponentClock(MediaType t, TimeSpan blockTime)
        {
            var offset = Container.Components[t]?.StartTime ?? TimeSpan.MinValue;
            offset = offset == TimeSpan.MinValue ? TimeSpan.Zero : offset;
            return TimeSpan.FromTicks(blockTime.Ticks - offset.Ticks);
        }

        /// <summary>
        /// Sends the given block to its corresponding media renderer.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="playbackPosition">The clock position.</param>
        /// <returns>
        /// The number of blocks sent to the renderer
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SendBlockToRenderer(MediaBlock block, TimeSpan playbackPosition)
        {
            // No blocks were rendered
            if (block == null) return 0;

            var t = block.MediaType;

            // Process property changes coming from video blocks
            State.UpdateDynamicBlockProperties(block, MediaCore.Blocks[t]);

            // Capture the last render time so we don't repeat the block
            MediaCore.LastRenderTime[t] = block.StartTime;

            // Send the block to its corresponding renderer
            MediaCore.Renderers[t]?.Render(block, playbackPosition);

            // Log the block statistics for debugging
            LogRenderBlock(block, playbackPosition, block.Index);

            // At this point, we are certain that a block has been
            // sent to its corresponding renderer. Make room so that the
            // decoder continues decoding frames
            var range = MediaCore.Blocks[t].GetRangePercent(playbackPosition);
            while (MediaCore.Blocks[t].Count >= 3 && range >= 0.666d)
            {
                if (MediaCore.Blocks[t][0] == block)
                    break;

                MediaCore.Blocks[t].RemoveFirst();
                range = MediaCore.Blocks[t].GetRangePercent(playbackPosition);
            }

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
