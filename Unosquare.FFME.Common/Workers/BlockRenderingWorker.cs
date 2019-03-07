﻿namespace Unosquare.FFME.Workers
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
    internal sealed class BlockRenderingWorker : ThreadWorkerBase, IMediaWorker, ILoggingSource
    {
        private readonly AtomicBoolean HasInitialized = new AtomicBoolean(false);
        private readonly Action<MediaType[]> SerialRenderBlocks;
        private readonly Action<MediaType[]> ParallelRenderBlocks;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockRenderingWorker"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public BlockRenderingWorker(MediaEngine mediaCore)
            : base(nameof(BlockRenderingWorker), Constants.ThreadWorkerPeriod)
        {
            MediaCore = mediaCore;
            Commands = MediaCore.Commands;
            Container = MediaCore.Container;
            MediaOptions = mediaCore.MediaOptions;
            State = MediaCore.State;
            ParallelRenderBlocks = (all) => Parallel.ForEach(all, (t) => RenderBlock(t));
            SerialRenderBlocks = (all) => { foreach (var t in all) RenderBlock(t); };
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
        /// Gets the media options.
        /// </summary>
        private MediaOptions MediaOptions { get; }

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
                if (MediaOptions.UseParallelRendering)
                    ParallelRenderBlocks.Invoke(all);
                else
                    SerialRenderBlocks.Invoke(all);
            }
            catch (Exception ex)
            {
                MediaCore.LogError(
                    Aspects.RenderingWorker, "Error while in rendering worker cycle", ex);

                throw;
            }
            finally
            {
                DetectPlaybackEnded(main);
                ExitSyncBuffering(main, all, ct);
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
        /// <param name="main">The main renderer component.</param>
        /// <param name="all">All the renderer components.</param>
        /// <returns>If media was initialized successfully.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Initialize(MediaType main, MediaType[] all)
        {
            // Don't run the cycle if we have already initialized
            if (HasInitialized == true)
                return true;

            if (!MediaOptions.DropLateFrames)
            {
                // wait for main component blocks or EOF or cancellation pending
                if (MediaCore.Blocks[main].Count <= 0)
                    return false;

                // Get the main stream's real start time
                var startTime = MediaCore.Blocks[main].RangeStartTime;

                // Check if the streams have related timing information
                // If they do not then simply forsce the disabling of time synchronization
                // and have the playback clock be independent for each component
                if (!MediaOptions.IsTimeSyncDisabled)
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
                            MediaOptions.IsTimeSyncDisabled = true;
                            this.LogWarning(Aspects.RenderingWorker,
                                $"{nameof(MediaOptions)}.{nameof(MediaOptions.IsTimeSyncDisabled)} has been set to true because the {main} and {t} " +
                                $"streams seem to have unrelated timing information. Time Difference: {startTimeOffset.Format()} s.");

                            break;
                        }
                    }
                }

                // Set the initial clock position
                MediaCore.ChangePlaybackPosition(startTime, false);
            }

            // Wait for renderers to be ready
            foreach (var t in all)
                MediaCore.Renderers[t]?.WaitForReadyState();

            // Mark as initialized
            HasInitialized.Value = true;
            return true;
        }

        /// <summary>
        /// Ensures the real-time clock does lag or move beyond the range of the main blocks
        /// </summary>
        /// <param name="main">The main renderer component.</param>
        /// <param name="all">All the renderer components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AlignWallClockToPlayback(MediaType main, MediaType[] all)
        {
            // we don't want to disturb the clock or align it to the main component if time-sync is disabled
            // or if drop late frames is enabled
            if (Commands.HasPendingCommands)
                return;

            // Get a reference to the main blocks.
            // The range will be 0 if there are no blocks.
            var blocks = MediaCore.Blocks[main];
            var range = !MediaOptions.DropLateFrames ? blocks.GetRangePercent(MediaCore.PlaybackClock()) : 0;

            if (range >= 1d)
            {
                // Don't let the RTC move beyond what is available on the main component
                MediaCore.ChangePlaybackPosition(blocks.RangeEndTime, false);
            }
            else if (range < 0)
            {
                // Don't let the RTC lag behind what is available on the main component
                MediaCore.ChangePlaybackPosition(blocks.RangeStartTime, false);
            }
            else if (range == 0 && blocks.Count == 0)
            {
                // We have no main blocks in range. All we can do is pause the clock
                MediaCore.PausePlayback(false);
            }
        }

        /// <summary>
        /// Enters the sync-buffering scenario if needed.
        /// </summary>
        /// <param name="main">The main renderer component.</param>
        /// <param name="all">All the renderer components.</param>
        /// <returns>Whether sync-buffering was entered</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EnterSyncBuffering(MediaType main, MediaType[] all)
        {
            // Determine if Sync-buffering can be potentially entered.
            // Entering the sync-buffering state pauses the RTC and forces the decoder make
            // components catch up with the main component.
            if (MediaCore.IsSyncBuffering || MediaOptions.DropLateFrames ||
                Commands.HasPendingCommands || !State.IsBuffering || !MediaCore.NeedsMorePackets)
            {
                return false;
            }

            var enterSyncBuffring = false;

            foreach (var t in all)
            {
                if (t == MediaType.Subtitle)
                    continue;

                // We don't need to do a thing if we are in range
                if (MediaCore.Blocks[t].IsInRange(MediaCore.PlaybackClock()))
                    continue;

                if (MediaOptions.IsTimeSyncDisabled)
                {
                    // If we don't have enough packets in any of the components component we need to
                    // enter sync-buffring
                    var minPacketBufferCount = Math.Min(MediaCore.Blocks[t].Capacity, Container.Components[t].BufferCountThreshold) / 2;
                    if (Container.Components[t].BufferCount < minPacketBufferCount)
                    {
                        enterSyncBuffring = true;
                        break;
                    }
                }
                else
                {
                    // If we are not in range of the non-main component we need to
                    // enter sync-buffering
                    enterSyncBuffring = true;
                    break;
                }
            }

            // If we have detected the start of a syncbuffering scenario
            // pause the playback and signal the new state.
            if (enterSyncBuffring)
            {
                MediaCore.PausePlayback(false);
                MediaCore.SignalSyncBufferingEntered();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Exits the sync-buffering state.
        /// </summary>
        /// <param name="main">The main renderer component.</param>
        /// <param name="all">All the renderer components.</param>
        /// <param name="ct">The cancellation token.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitSyncBuffering(MediaType main, MediaType[] all, CancellationToken ct)
        {
            // Don't exit syc-buffering if we are not in syncbuffering
            if (!MediaCore.IsSyncBuffering)
                return;

            // Detect if an exit from Sync Buffering is required
            var canExitSyncBuffering = MediaCore.Blocks[main].Count > 0;
            var mustExitSyncBuffering = ct.IsCancellationRequested || MediaCore.HasDecodingEnded ||
                Commands.HasPendingCommands || MediaOptions.DropLateFrames;

            try
            {
                if (mustExitSyncBuffering)
                {
                    this.LogDebug(Aspects.ReadingWorker, $"SYNC-BUFFER: 'must exit' condition met.");
                    return;
                }

                if (!canExitSyncBuffering)
                {
                    this.LogDebug(Aspects.ReadingWorker, $"SYNC-BUFFER: Ubable to exit sync-buffering. {main} has no blocks.");
                    return;
                }

                foreach (var t in all)
                {
                    if (t == MediaType.Subtitle)
                        continue;

                    if (MediaOptions.IsTimeSyncDisabled)
                    {
                        if (!Container.Components[t].HasEnoughPackets)
                        {
                            canExitSyncBuffering = false;
                            break;
                        }
                    }
                    else
                    {
                        if (MediaCore.Blocks[t].GetRangePercent(MediaCore.PlaybackClock()) > 0.75d)
                        {
                            canExitSyncBuffering = false;
                            break;
                        }
                    }
                }
            }
            finally
            {
                // Exit sync-buffering state if we can or we must
                if (mustExitSyncBuffering || canExitSyncBuffering)
                {
                    var blocks = MediaCore.Blocks[main];
                    var playbackPosition = MediaCore.PlaybackClock(main);
                    if (blocks.Count > 0 && !blocks.IsInRange(playbackPosition))
                        MediaCore.ChangePlaybackPosition(blocks[playbackPosition].StartTime, false);

                    MediaCore.SignalSyncBufferingExited();
                }
            }
        }

        /// <summary>
        /// Waits for seek blocks to become available.
        /// </summary>
        /// <param name="main">The main renderer component.</param>
        /// <param name="ct">The cancellation token.</param>
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

        /// <summary>
        /// Renders the available, non-repeated block.
        /// </summary>
        /// <param name="t">The media type.</param>
        /// <returns>Whether a block was sent to its corresponding renderer</returns>
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

        /// <summary>
        /// Detects whether the playback has ended.
        /// </summary>
        /// <param name="main">The main component type.</param>
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
                    MediaCore.PausePlayback(true);
                    var endPosition = MediaCore.ChangePlaybackPosition(playbackEndClock, true);
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
        /// Reports the playback position if needed and
        /// resumes the playback clock if required.
        /// </summary>
        /// <param name="main">The main.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdatePlayback(MediaType main)
        {
            // Notify a change in playback position
            if (Commands.HasPendingCommands == false && !MediaCore.IsSyncBuffering)
                State.ReportPlaybackPosition();

            // Resume the RTC if necessary
            if (State.MediaState != PlaybackStatus.Play || Commands.HasPendingCommands)
                return;

            // Force playback no matter what because clock cannot be paused
            if (!MediaCore.IsClockPauseable)
            {
                MediaCore.Clock.Play();
                return;
            }

            // We don't want to resume the clock if we are not ready for playback
            if (MediaCore.IsSyncBuffering || MediaCore.Blocks[main].Count <= 0)
                return;

            // Resume the clock
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
