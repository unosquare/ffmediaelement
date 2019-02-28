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
            var wallClock = MediaCore.WallClock;
            var playbackClock = MediaCore.PlaybackClock;

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
                #region 1. Clock Control Logic

                if (MediaCore.State.MediaState == PlaybackStatus.Play)
                {
                    var range = MediaCore.Blocks[main].GetRangePercent(MediaCore.PlaybackClock);
                    if (range > 1d)
                    {
                        // Don't let the RTC move beyond what is available on the main component
                        MediaCore.Clock.Pause();
                        MediaCore.Clock.Update(MediaCore.ConvertPlaybackToClockTime(MediaCore.Blocks[main].RangeEndTime));
                    }
                    else if (range < 0)
                    {
                        // Don't let the RTC lag behind what is available on the main component
                        MediaCore.Clock.Update(MediaCore.ConvertPlaybackToClockTime(MediaCore.Blocks[main].RangeStartTime));
                    }
                    else
                    {
                        MediaCore.Clock.Play();
                    }
                }

                #endregion

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
                wallClock = MediaCore.WallClock;
                playbackClock = MediaCore.ConvertClockToPlaybackTime(wallClock);

                if (MediaCore.Clock.IsRunning)
                {
                    foreach (var t in all)
                    {
                        if (t != MediaType.Video) continue;
                        var info = new BlockInfo(MediaCore, t, wallClock);
                        if (info.BlockShouldRender)
                            Console.WriteLine(info.ToString());
                    }
                }

                foreach (var t in all)
                {
                    // skip blocks if we are seeking and they are not video blocks
                    if (Commands.IsSeeking && t != MediaType.Video)
                    {
                        currentBlock[t] = null;
                        continue;
                    }

                    var componentClock = MediaCore.ConvertPlaybackToComponentClock(t, playbackClock);

                    // Get the audio, video, or subtitle block to render
                    currentBlock[t] = t == MediaType.Subtitle && MediaCore.PreloadedSubtitles != null ?
                        MediaCore.PreloadedSubtitles[componentClock] :
                        MediaCore.Blocks[t][componentClock];
                }

                // Render each of the Media Types if it is time to do so.
                foreach (var t in all)
                {
                    // Don't send null or disposed blocks to renderer
                    if (currentBlock[t] == null || currentBlock[t].IsDisposed)
                        continue;

                    // Render by forced signal (TimeSpan.MinValue) or because simply it is time to do so
                    if (MediaCore.LastRenderTime[t] == TimeSpan.MinValue || currentBlock[t].StartTime != MediaCore.LastRenderTime[t])
                        SendBlockToRenderer(currentBlock[t], playbackClock);

                    // TODO: Maybe SendBlockToRenderer repeatedly for contiguous audio blocks
                    // Also remove the logic where the renderer reads the contiguous audio frames
                }

                #endregion

                #region 3. Finalize the Rendering Cycle

                // Call the update method on all renderers so they receive what the new wall clock is.
                foreach (var t in all)
                    MediaCore.Renderers[t]?.Update(playbackClock);

                #endregion
            }
            catch (Exception ex)
            {
                MediaCore.LogError(Aspects.RenderingWorker, "Error while in rendering worker cycle", ex);
                throw;
            }
            finally
            {
                var playbackEndClock = MediaCore.ConvertPlaybackToClockTime(MediaType.None, MediaCore.Blocks[main].RangeEndTime);

                // Check End of Media Scenarios
                if (Commands.IsSeeking == false
                && MediaCore.HasDecodingEnded
                && MediaCore.WallClock.Ticks >= playbackEndClock.Ticks)
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
                        var endPosition = MediaCore.ChangePosition(playbackEndClock);
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
            LogRenderBlock(block, playbackPosition, block.Index);

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

        private sealed class BlockInfo
        {
            public BlockInfo(MediaEngine mediaCore, MediaType t, TimeSpan wallClock)
            {
                MediaType = t;
                WallClock = wallClock;
                PlaybackStartTime = mediaCore.GetComponentStartOffset(MediaType.None);
                PlaybackEndTime = mediaCore.Container.Components.PlaybackEndTime ?? TimeSpan.Zero;
                PlaybackDuration = mediaCore.Container.Components.PlaybackDuration ?? TimeSpan.Zero;
                PlaybackClock = mediaCore.ConvertClockToPlaybackTime(MediaType.None, WallClock);
                ComponentStartTime = mediaCore.GetComponentStartOffset(t);
                ComponentClock = mediaCore.ConvertPlaybackToComponentClock(t, PlaybackClock);

                var blocks = mediaCore.Blocks[t];
                var block = blocks[ComponentClock];

                BlockStartTime = block?.StartTime ?? TimeSpan.Zero;
                BlockIndex = block?.Index ?? -1;
                BlockRangePercent = blocks.GetRangePercent(PlaybackClock);
                BlockCapacityPercent = blocks.CapacityPercent;
                BlockCount = blocks.Count;
                BlockIsInRange = blocks.IsInRange(PlaybackClock);

                BlockShouldRender = mediaCore.LastRenderTime[t] == null
                    ? false
                    : mediaCore.LastRenderTime[t] == TimeSpan.MinValue || mediaCore.LastRenderTime[t] != BlockStartTime ? true : false;
            }

            public MediaType MediaType { get; }

            public TimeSpan WallClock { get; }

            public TimeSpan PlaybackStartTime { get; }

            public TimeSpan PlaybackEndTime { get; }

            public TimeSpan PlaybackDuration { get; }

            public TimeSpan PlaybackClock { get; }

            public TimeSpan ComponentStartTime { get; }

            public TimeSpan ComponentClock { get; }

            public TimeSpan BlockStartTime { get; }

            public int BlockIndex { get; }

            public double BlockRangePercent { get; }

            public double BlockCapacityPercent { get; }

            public int BlockCount { get; }

            public bool BlockIsInRange { get; }

            public bool BlockShouldRender { get; }

            public override string ToString()
            {
                return $"{MediaType,-9} | Wall: {WallClock.Format()}"
                    + $" | Media Start: {PlaybackStartTime.Format()}"
                    + $" | Media End: {PlaybackEndTime.Format()}"
                    + $" | Media Duration: {PlaybackDuration.Format()}"
                    + $" | Media Current: {PlaybackClock.Format()}"
                    + $" | Comp Start: {ComponentStartTime.Format()}"
                    + $" | Comp Current: {ComponentClock.Format()}"
                    + $" | Block Start: {BlockStartTime.Format()}"
                    + $" | Block Index: {BlockIndex}"
                    + $" | Block Range: {BlockRangePercent:p2}"
                    + $" | Block Fill: {BlockCapacityPercent:p2}"
                    + $" | Block Count: {BlockCount,-4}"
                    + $" | Block In Range: {BlockIsInRange}"
                    + $" | Block Should Render: {BlockShouldRender}";
            }
        }
    }
}
