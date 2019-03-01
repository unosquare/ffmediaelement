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

        /// <summary>
        /// Whether or not blocks should be sent to their renderers in parallel.
        /// </summary>
        private bool UseParallelRendering { get; }

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            // Update Status Properties
            var main = Container.Components.MainMediaType;
            var all = MediaCore.Renderers.Keys.ToArray();
            var hasEnteredSyncBuffering = false;

            try
            {
                // Ensure we have renderers ready and main blocks available
                if (!Initialize(main, all)) return;

                // Ensure we have sufficient blocks on all components
                // and the clock is in the proper state
                if (!EnsureBlocksAvailable(main, all)) return;

                hasEnteredSyncBuffering = EnterSyncBuffering(main, all);

                // If we are in the middle of a seek, wait for seek blocks
                WaitForSeekBlocks(main, ct);

                // Render each of the Media Types if it is time to do so.
                if (UseParallelRendering)
                {
                    Parallel.ForEach(all, (t) =>
                        RenderBlock(t));
                }
                else
                {
                    foreach (var t in all)
                        RenderBlock(t);
                }
            }
            catch (Exception ex)
            {
                MediaCore.LogError(
                    Aspects.RenderingWorker, "Error while in rendering worker cycle", ex);

                throw;
            }
            finally
            {
                // always ensure we run the clock if we need a play status
                // TODO: the rendering worker must always control the clock -- find where this is not the case
                // all references to updating the clock or resuming playback must be removed
                if (MediaCore.State.MediaState == PlaybackStatus.Play && !MediaCore.IsSyncBuffering && !Commands.IsSeeking && !hasEnteredSyncBuffering)
                    MediaCore.Clock.Play();

                ExitSyncBuffering(main, ct);

                // Detect end of media scenarios
                DetectHasMediaEnded(main);

                // Update the Position
                if (Commands.IsSeeking == false)
                    State.ReportPlaybackPosition();
            }
        }

        /// <inheritdoc />
        protected override void OnCycleException(Exception ex) =>
            this.LogError(Aspects.RenderingWorker, "Worker Cycle exception thrown", ex);

        /// <inheritdoc />
        protected override void OnDisposing()
        {
            // Reset the state to non-sync-buffering
            MediaCore.SignalSyncBufferingExited();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Initialize(MediaType main, MediaType[] all)
        {
            if (HasInitialized == true) return true;

            // wait for main component blocks or EOF or cancellation pending
            if (MediaCore.Blocks[main].Count <= 0)
                return false;

            var startTime = MediaCore.Blocks[main].RangeStartTime;

            // Set the initial clock position
            MediaCore.State.UpdatePlaybackStartTime(startTime);
            MediaCore.ChangePlaybackPosition(startTime);

            // Wait for renderers to be ready
            foreach (var t in all)
                MediaCore.Renderers[t]?.WaitForReadyState();

            // Mark as initialized
            HasInitialized.Value = true;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EnsureBlocksAvailable(MediaType main, MediaType[] all)
        {
            if (Commands.IsSeeking)
                return true;

            var blocks = MediaCore.Blocks[main];
            var range = blocks.GetRangePercent(MediaCore.PlaybackClock(MediaType.None));

            if (range >= 1d)
            {
                // Don't let the RTC move beyond what is available on the main component
                // MediaCore.PausePlayback();
                MediaCore.ChangePlaybackPosition(blocks.RangeEndTime);
            }
            else if (range < 0)
            {
                // Don't let the RTC lag behind what is available on the main component
                MediaCore.ChangePlaybackPosition(blocks.RangeStartTime);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EnterSyncBuffering(MediaType main, MediaType[] all)
        {
            // TODO: This is anew approach to sync-buffering
            // it still needs work -- see prior sync-buffering logic
            if (MediaCore.IsSyncBuffering || !MediaCore.NeedsMorePackets || State.BufferingProgress >= 1d)
                return false;

            foreach (var t in all)
            {
                if (t == main || t == MediaType.Subtitle)
                    continue;

                if (MediaCore.Blocks[t].IsInRange(MediaCore.PlaybackClock(t)))
                {
                    MediaCore.PausePlayback();
                    MediaCore.SignalSyncBufferingEntered();
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ExitSyncBuffering(MediaType main, CancellationToken ct)
        {
            // Don't exit syc-buffering if we are not in syncbuffering
            if (!MediaCore.IsSyncBuffering)
                return false;

            // Detect if an exit from Sync Buffering is required
            var mustExitSyncBuffering =
                ct.IsCancellationRequested ||
                MediaCore.HasDecodingEnded ||
                State.BufferingProgress >= 0.95 ||
                !MediaCore.NeedsMorePackets;

            if (!mustExitSyncBuffering)
                return false;

            var blocks = MediaCore.Blocks[main];
            if (blocks.Count > 0 && !blocks.IsInRange(MediaCore.PlaybackClock(main)))
                MediaCore.ChangePlaybackPosition(blocks[MediaCore.PlaybackClock(main)].StartTime);

            MediaCore.SignalSyncBufferingExited();

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WaitForSeekBlocks(MediaType main, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested
            && Commands.IsActivelySeeking
            && !MediaCore.Blocks[main].IsInRange(MediaCore.PlaybackClock(main)))
            {
                // Check if we finally have seek blocks available
                // if we don't get seek blocks in range and we are not step-seeking,
                // then we simply break out of the loop and render whatever it is we have
                // to create the illussion of smooth seeking. For precision seeking we
                // continue the loop.
                if (Commands.ActiveSeekMode == CommandManager.SeekMode.Normal
                && !Commands.WaitForSeekBlocks(DefaultPeriod.Milliseconds))
                {
                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool RenderBlock(MediaType t)
        {
            var result = 0;
            var playbackClock = MediaCore.PlaybackClock(t);

            try
            {
                // We don't need non-video blocks if we are seeking
                if (Commands.IsSeeking && t != MediaType.Video)
                    return result > 0;

                // Get the audio, video, or subtitle block to render
                var currentBlock = t == MediaType.Subtitle && MediaCore.PreloadedSubtitles != null
                    ? MediaCore.PreloadedSubtitles[playbackClock]
                    : MediaCore.Blocks[t][playbackClock];

                // Send the block to the corresponding renderer
                // this will handle fringe and skip cases
                result += SendBlockToRenderer(currentBlock, playbackClock);
            }
            finally
            {
                // Call the update method on all renderers so they receive what the new playback clock is.
                MediaCore.Renderers[t]?.Update(playbackClock);
            }

            return result > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DetectHasMediaEnded(MediaType main)
        {
            var playbackEndClock = MediaCore.Blocks[main].Count > 0
                ? MediaCore.Blocks[main].RangeEndTime
                : Container.Components.PlaybackEndTime ?? TimeSpan.MaxValue;

            // Check End of Media Scenarios
            if (Commands.IsSeeking == false
                && MediaCore.HasDecodingEnded
                && MediaCore.PlaybackClock(MediaType.None).Ticks >= playbackEndClock.Ticks)
            {
                // Rendered all and nothing else to render
                if (State.HasMediaEnded == false)
                {
                    MediaCore.PausePlayback();

                    // TODO: compute end update end position
                    // this will get overwritten when changemedia gets called
                    // maybe we need to save it separately
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
        }

        /// <summary>
        /// Sends the given block to its corresponding media renderer.
        /// </summary>
        /// <param name="currentBlock">The block.</param>
        /// <param name="playbackPosition">The clock position.</param>
        /// <returns>
        /// The number of blocks sent to the renderer
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SendBlockToRenderer(MediaBlock currentBlock, TimeSpan playbackPosition)
        {
            // No blocks were rendered
            if (currentBlock == null || currentBlock.IsDisposed) return 0;

            var t = currentBlock.MediaType;
            var lastBlockStartTime = MediaCore.LastRenderTime[t];

            // Render by forced signal (TimeSpan.MinValue) or because simply it is time to do so
            // otherwise simply skip block rendering as we have sent the block already.
            if (lastBlockStartTime != TimeSpan.MinValue && lastBlockStartTime == currentBlock.StartTime)
                return 0;

            // Process property changes coming from video blocks
            State.UpdateDynamicBlockProperties(currentBlock, MediaCore.Blocks[t]);

            // Capture the last render time so we don't repeat the block
            MediaCore.LastRenderTime[t] = currentBlock.StartTime;

            // Send the block to its corresponding renderer
            MediaCore.Renderers[t]?.Render(currentBlock, playbackPosition);

            // Log the block statistics for debugging
            LogRenderBlock(currentBlock, playbackPosition);

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
                this.LogTrace(Aspects.RenderingWorker,
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
