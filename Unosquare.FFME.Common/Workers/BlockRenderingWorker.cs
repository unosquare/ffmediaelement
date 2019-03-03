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
        private readonly Action<MediaType[]> RenderBlocksAction;

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

            if (UseParallelRendering)
                RenderBlocksAction = (all) => Parallel.ForEach(all, (t) => RenderBlock(t));
            else
                RenderBlocksAction = (all) => { foreach (var t in all) RenderBlock(t); };
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

            // Ensure we have renderers ready and main blocks available
            if (!Initialize(main, all))
                return;

            try
            {
                // If we are in the middle of a seek, wait for seek blocks
                WaitForSeekBlocks(main, ct);

                // Ensure the RTC clock matches the playback position of the
                // main component -- only if IsTimeSyncDisabled is false
                AlignWallClockToPlayback(main, all);

                // Check for and enter a sync-buffering scenario
                EnterSyncBuffering(main, all);

                // Render each of the Media Types if it is time to do so.
                RenderBlocksAction.Invoke(all);
            }
            catch (Exception ex)
            {
                MediaCore.LogError(
                    Aspects.RenderingWorker, "Error while in rendering worker cycle", ex);

                throw;
            }
            finally
            {
                ExitSyncBuffering(main, all, ct);
                DetectPlaybackEnded(main);
                UpdatePlayback(main);
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

        /// <summary>
        /// Performs initialization before regular render loops are executed.
        /// </summary>
        /// <param name="main">The main renderer media type.</param>
        /// <param name="all">All renderer media types.</param>
        /// <returns>If media was initialized successfully.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Initialize(MediaType main, MediaType[] all)
        {
            // Don't run the cycle if we have already initialized
            if (HasInitialized == true)
                return true;

            // wait for main component blocks or EOF or cancellation pending
            if (MediaCore.Blocks[main].Count <= 0)
                return false;

            // Get the main stream's real start time
            var startTime = MediaCore.Blocks[main].RangeStartTime;

            // Check if the streams have related timing information
            // If they do not then simply forsce the disabling of time synchronization
            // and have the playback clock be independent for each component
            if (!Container.MediaOptions.IsTimeSyncDisabled)
            {
                foreach (var t in all)
                {
                    // Check if the component is applicable to time synchronization to main
                    if (t == main || t == MediaType.Subtitle || MediaCore.Blocks[t] == null)
                        continue;

                    // If we have not received any blocks yet we con't compare
                    // start time differences.
                    if (MediaCore.Blocks[t].Count == 0)
                        return false;

                    // Compute the offset difference with respect to the main component
                    var startTimeOffset = TimeSpan.FromTicks(
                        Math.Abs(MediaCore.Blocks[t][0].StartTime.Ticks - startTime.Ticks));

                    // Disable TimeSync if the streams are unrelated.
                    if (startTimeOffset > Constants.TimeSyncMaxOffset)
                    {
                        Container.MediaOptions.IsTimeSyncDisabled = true;
                        this.LogWarning(Aspects.RenderingWorker,
                            $"{nameof(MediaOptions)}.{nameof(MediaOptions.IsTimeSyncDisabled)} has been set to true because the {main} and {t} " +
                            $"streams seem to have unrelated timing information. Difference: {startTimeOffset.Format()}");

                        break;
                    }
                }
            }

            // Set the initial clock position
            State.UpdatePlaybackStartTime(startTime);
            MediaCore.ChangePlaybackPosition(startTime);

            // Wait for renderers to be ready
            foreach (var t in all)
                MediaCore.Renderers[t]?.WaitForReadyState();

            // Mark as initialized
            HasInitialized.Value = true;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AlignWallClockToPlayback(MediaType main, MediaType[] all)
        {
            if (Commands.HasPendingCommands || Container.MediaOptions.IsTimeSyncDisabled)
                return;

            // Get a reference to the main blocks.
            // The range will be 0 if there are no blocks.
            var blocks = MediaCore.Blocks[main];
            var range = blocks.GetRangePercent(MediaCore.PlaybackClock());

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
            else if (range == 0 && blocks.Count == 0)
            {
                // We have no main blocks in range. All we can do is pause the clock
                MediaCore.PausePlayback();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EnterSyncBuffering(MediaType main, MediaType[] all)
        {
            // Determine if Sync-buffering can be potentially entered.
            if (MediaCore.IsSyncBuffering || Container.MediaOptions.IsTimeSyncDisabled ||
                Commands.HasPendingCommands || State.BufferingProgress >= 1d || !MediaCore.NeedsMorePackets)
            {
                return false;
            }

            // If it can be entered, then let's check if we really need it.
            foreach (var t in all)
            {
                // We can't enter sync-buffering on main or subtitle components
                // The playback clock is always reflective of the main component
                if (t == main || t == MediaType.Subtitle)
                    continue;

                // If we are not in range of the non-main component we need to
                // enter sync-buffering
                if (!MediaCore.Blocks[t].IsInRange(MediaCore.PlaybackClock(t)))
                {
                    MediaCore.PausePlayback();
                    MediaCore.SignalSyncBufferingEntered();
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ExitSyncBuffering(MediaType main, MediaType[] all, CancellationToken ct)
        {
            // Don't exit syc-buffering if we are not in syncbuffering
            if (!MediaCore.IsSyncBuffering)
                return false;

            // Detect if an exit from Sync Buffering is required
            var mustExitSyncBuffering = ct.IsCancellationRequested || MediaCore.HasDecodingEnded || Commands.HasPendingCommands;
            var canExitSyncBuffering = false;

            if (!mustExitSyncBuffering && (State.BufferingProgress >= 1 || !MediaCore.NeedsMorePackets))
            {
                // In order to exit sync-buffering we at least need 1 main block.
                canExitSyncBuffering = MediaCore.Blocks[main].Count > 0;

                foreach (var t in all)
                {
                    if (canExitSyncBuffering == false)
                        break;

                    if (t == MediaType.Subtitle || t == main)
                        continue;

                    if (MediaCore.Blocks[t].GetRangePercent(MediaCore.PlaybackClock(t)) > 0.75d)
                    {
                        canExitSyncBuffering = false;
                        break;
                    }
                }
            }

            if (mustExitSyncBuffering || canExitSyncBuffering)
            {
                var blocks = MediaCore.Blocks[main];
                if (blocks.Count > 0 && !blocks.IsInRange(MediaCore.PlaybackClock(main)))
                    MediaCore.ChangePlaybackPosition(blocks[MediaCore.PlaybackClock(main)].StartTime);

                MediaCore.SignalSyncBufferingExited();
                return true;
            }

            return false;
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
                if (Commands.HasPendingCommands && t != MediaType.Video)
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
        private void DetectPlaybackEnded(MediaType main)
        {
            var playbackEndClock = MediaCore.Blocks[main].Count > 0
                ? MediaCore.Blocks[main].RangeEndTime
                : Container.Components.PlaybackEndTime ?? TimeSpan.MaxValue;

            // Check End of Media Scenarios
            if (Commands.HasPendingCommands == false
                && MediaCore.HasDecodingEnded
                && MediaCore.PlaybackClock(main).Ticks >= playbackEndClock.Ticks)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdatePlayback(MediaType main)
        {
            // Notify a change in playback position
            if (Commands.HasPendingCommands == false)
                State.ReportPlaybackPosition();

            // Resume the RTC if necessary
            if (State.MediaState != PlaybackStatus.Play || MediaCore.Clock.IsRunning)
                return;

            if (MediaCore.IsSyncBuffering || Commands.HasPendingCommands)
                return;

            if (Container.MediaOptions.IsTimeSyncDisabled || MediaCore.Blocks[main].Count > 0)
                MediaCore.Clock.Play();
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
