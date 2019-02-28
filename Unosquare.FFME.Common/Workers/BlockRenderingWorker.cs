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
    using System.Threading.Tasks;

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
            var playbackClock = MediaCore.PlaybackClock;

            if (HasInitialized == false)
            {
                // wait for main component blocks or EOF or cancellation pending
                if (MediaCore.Blocks[main].Count <= 0)
                    return;

                var startTime = MediaCore.Blocks[main].RangeStartTime;

                // Set the initial clock position
                MediaCore.State.UpdatePlaybackStartTime(startTime);
                MediaCore.ChangePlaybackPosition(startTime);

                // Wait for renderers to be ready
                foreach (var t in all)
                    MediaCore.Renderers[t]?.WaitForReadyState();

                // Mark as initialized
                HasInitialized.Value = true;
            }

            #region Run the Rendering Cycle

            try
            {
                #region 0. Clock Control Logic

                // Control the clock based on
                if (Commands.IsSeeking == false)
                {
                    var blocks = MediaCore.Blocks[main];
                    var range = blocks.GetRangePercent(playbackClock);

                    if (range >= 1d)
                    {
                        // Don't let the RTC move beyond what is available on the main component
                        MediaCore.Clock.Pause();
                        MediaCore.ChangePlaybackPosition(blocks.RangeEndTime);
                    }
                    else if (range < 0)
                    {
                        // Don't let the RTC lag behind what is available on the main component
                        MediaCore.ChangePlaybackPosition(blocks.RangeStartTime);
                    }

                    // TODO: This is anew approach to sync-buffering
                    if (MediaCore.ShouldReadMorePackets)
                    {
                        foreach (var t in all)
                        {
                            if (t == main || t == MediaType.Subtitle)
                                continue;

                            range = MediaCore.Blocks[t].GetRangePercent(playbackClock);

                            if (State.BufferingProgress < 1 && (range < 0 || range >= 1))
                            {
                                MediaCore.Clock.Pause();
                                return;
                            }
                        }
                    }

                    // always ensure we run the clock if we need a play status
                    // TODO the rendering worker must always control the clock
                    if (MediaCore.State.MediaState == PlaybackStatus.Play)
                        MediaCore.Clock.Play();
                }

                #endregion

                #region 1. Wait for any seek operation to make blocks available in a loop

                while (!ct.IsCancellationRequested
                && Commands.IsActivelySeeking
                && !MediaCore.Blocks[main].IsInRange(MediaCore.PlaybackClock))
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
                playbackClock = MediaCore.PlaybackClock;

                // Render each of the Media Types if it is time to do so.
                Parallel.ForEach(all, (t) =>
                {
                    try
                    {
                        // We don't need non-video blocks if we are seeking
                        if (Commands.IsSeeking && t != MediaType.Video)
                            return;

                        // Get the audio, video, or subtitle block to render
                        var currentBlock = t == MediaType.Subtitle && MediaCore.PreloadedSubtitles != null
                            ? MediaCore.PreloadedSubtitles[playbackClock]
                            : MediaCore.Blocks[t][playbackClock];

                        // Don't send null or disposed blocks to renderer
                        if (currentBlock == null || currentBlock.IsDisposed)
                            return;

                        // Render by forced signal (TimeSpan.MinValue) or because simply it is time to do so
                        var lastBlockStartTime = MediaCore.LastRenderTime[t];
                        if (lastBlockStartTime != currentBlock.StartTime || lastBlockStartTime == TimeSpan.MinValue)
                            SendBlockToRenderer(currentBlock, playbackClock);
                    }
                    finally
                    {
                        // Call the update method on all renderers so they receive what the new playback clock is.
                        MediaCore.Renderers[t]?.Update(playbackClock);
                    }
                });

                #endregion
            }
            catch (Exception ex)
            {
                MediaCore.LogError(Aspects.RenderingWorker, "Error while in rendering worker cycle", ex);
                throw;
            }
            finally
            {
                var playbackEndClock = MediaCore.Blocks[main].Count > 0
                    ? MediaCore.Blocks[main].RangeEndTime
                    : Container.Components.PlaybackEndTime ?? TimeSpan.MaxValue;

                // Check End of Media Scenarios
                if (Commands.IsSeeking == false
                && MediaCore.HasDecodingEnded
                && MediaCore.PlaybackClock.Ticks >= playbackEndClock.Ticks)
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
                        var endPosition = MediaCore.ChangePlaybackPosition(playbackEndClock);
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
                    State.ReportPlaybackPosition();
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
            LogRenderBlock(block, playbackPosition);

            return 1;
        }

        /// <summary>
        /// Logs a block rendering operation as a Trace Message
        /// if the debugger is attached.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogRenderBlock(MediaBlock block, TimeSpan clockPosition)
        {
            // Prevent logging for production use
            if (MediaEngine.Platform.IsInDebugMode == false) return;

            try
            {
                var drift = TimeSpan.FromTicks(clockPosition.Ticks - block.StartTime.Ticks);
                this.LogInfo(Aspects.RenderingWorker,
                    $"{block.MediaType.ToString().Substring(0, 1)} "
                    + $"BLK: {block.StartTime.Format()} | "
                    + $"CLK: {clockPosition.Format()} | "
                    + $"DFT: {drift.TotalMilliseconds,4:0} | "
                    + $"IX: {block.Index,3} | "
                    + $"RNG: {MediaCore.Blocks[block.MediaType].GetRangePercent(clockPosition):p} | "
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
