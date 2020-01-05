namespace Unosquare.FFME.Engine
{
    using Commands;
    using Common;
    using Container;
    using Diagnostics;
    using Primitives;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements the block rendering worker.
    /// </summary>
    /// <seealso cref="IMediaWorker" />
    internal sealed class BlockRenderingWorker : WorkerBase, IMediaWorker, ILoggingSource
    {
        private readonly AtomicBoolean HasInitialized = new AtomicBoolean(false);
        private readonly Action SerialRenderBlocks;
        private readonly Action ParallelRenderBlocks;
        private readonly Thread QuantumThread;
        private readonly ManualResetEventSlim QuantumWaiter = new ManualResetEventSlim(false);
        private DateTime LastSpeedRatioTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockRenderingWorker"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public BlockRenderingWorker(MediaEngine mediaCore)
            : base(nameof(BlockRenderingWorker))
        {
            MediaCore = mediaCore;
            Commands = MediaCore.Commands;
            Container = MediaCore.Container;
            MediaOptions = mediaCore.MediaOptions;
            State = MediaCore.State;
            ParallelRenderBlocks = () => Parallel.ForEach(AllComponents, (t) => RenderBlock(t));
            SerialRenderBlocks = () => { foreach (var t in AllComponents) RenderBlock(t); };

            QuantumThread = new Thread(RunQuantumThread)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = $"{nameof(BlockRenderingWorker)}.Thread",
            };

            QuantumThread.Start();
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
        /// Gets a value indicating whether the component clocks are not bound together.
        /// </summary>
        private bool HasDisconnectedClocks => Timing.HasDisconnectedClocks;

        /// <summary>
        /// Gets the Media Engine's state.
        /// </summary>
        private MediaEngineState State { get; }

        /// <summary>
        /// Shortcut to container's components.
        /// </summary>
        private MediaComponentSet Components => Container.Components;

        /// <summary>
        /// Shortcut to the media engine's blocks.
        /// </summary>
        private MediaTypeDictionary<MediaBlockBuffer> Blocks => MediaCore.Blocks;

        /// <summary>
        /// Shortcut to the media engine's main component blocks.
        /// </summary>
        private MediaBlockBuffer MainBlocks => Blocks[MainMediaType];

        /// <summary>
        /// Shortcut to the media engine's timing controller.
        /// </summary>
        private TimingController Timing => MediaCore.Timing;

        /// <summary>
        /// Gets the remaining cycle time.
        /// </summary>
        private TimeSpan RemainingCycleTime
        {
            get
            {
                const double MaxFrameDuration = 50d;
                const double MinFrameDuration = 10d;

                try
                {
                    var frameDuration = MainMediaType == MediaType.Video && MainBlocks.Count > 0
                        ? MainBlocks.AverageBlockDuration
                        : Constants.DefaultTimingPeriod;

                    // protect against too slow or too fast of a video framerate
                    // which might impact audio rendering.
                    frameDuration = frameDuration.Clamp(
                        TimeSpan.FromMilliseconds(MinFrameDuration),
                        TimeSpan.FromMilliseconds(MaxFrameDuration));

                    return TimeSpan.FromTicks(frameDuration.Ticks - CurrentCycleElapsed.Ticks);
                }
                catch
                {
                    // ignore
                }

                return Constants.DefaultTimingPeriod;
            }
        }

        /// <summary>
        /// Gets or sets the main component.
        /// Set at the start of every cycle.
        /// </summary>
        private MediaComponent Main { get; set; }

        /// <summary>
        /// Gets or sets the main component type.
        /// Set at the start of every cycle.
        /// </summary>
        private MediaType MainMediaType { get; set; }

        /// <summary>
        /// Gets or sets all the component types.
        /// Set at the start of every cycle.
        /// </summary>
        private MediaType[] AllComponents { get; set; }

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            // Update Status Properties
            Main = Components.Main;
            MainMediaType = Components.MainMediaType;
            AllComponents = MediaCore.Renderers.Keys.ToArray();

            // Ensure we have renderers ready and main blocks available
            if (!Initialize())
                return;

            try
            {
                // If we are in the middle of a seek, wait for seek blocks
                WaitForSeekBlocks(ct);

                // Ensure the RTC clocks match the playback position
                AlignClocksToPlayback();

                // Check for and enter a sync-buffering scenario
                EnterSyncBuffering();

                // Render each of the Media Types if it is time to do so.
                if (MediaOptions.UseParallelRendering)
                    ParallelRenderBlocks.Invoke();
                else
                    SerialRenderBlocks.Invoke();
            }
            catch (Exception ex)
            {
                MediaCore.LogError(
                    Aspects.RenderingWorker, "Error while in rendering worker cycle", ex);

                throw;
            }
            finally
            {
                DetectPlaybackEnded();

                // CatchUpWithLiveStream(); // TODO: We are on to something good here
                ExitSyncBuffering(ct);
                ReportAndResumePlayback();
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

        /// <inheritdoc />
        protected override void Dispose(bool alsoManaged)
        {
            base.Dispose(alsoManaged);
            QuantumWaiter.Dispose();
        }

        /// <summary>
        /// Executes render thread logic in a cycle.
        /// </summary>
        private void RunQuantumThread(object state)
        {
            using var vsync = new VerticalSyncContext();
            while (WorkerState != WorkerState.Stopped)
            {
                if (!VerticalSyncContext.IsAvailable)
                    State.VerticalSyncEnabled = false;

                var performVersticalSyncWait =
                    Components.HasVideo &&
                    Timing.GetIsRunning(MediaType.Video) &&
                    State.VerticalSyncEnabled;

                if (performVersticalSyncWait)
                {
                    // wait a few times as there is no need to move on to the next frame
                    // if the remaining cycle time is more than twice the refresh rate.
                    while (RemainingCycleTime.Ticks >= vsync.RefreshPeriod.Ticks * 2)
                        vsync.WaitForBlank();

                    // wait one last time for the actual v-sync
                    if (RemainingCycleTime.Ticks > 0)
                        vsync.WaitForBlank();
                }
                else
                {
                    // Perform a synthetic wait
                    var waitTime = RemainingCycleTime;
                    if (waitTime.Ticks > 0)
                        QuantumWaiter.Wait(waitTime);
                }

                if (!TryBeginCycle())
                    continue;

                ExecuteCyle();
            }
        }

        /// <summary>
        /// Performs initialization before regular render loops are executed.
        /// </summary>
        /// <returns>If media was initialized successfully.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Initialize()
        {
            // Don't run the cycle if we have already initialized
            if (HasInitialized == true)
                return true;

            // Wait for renderers to be ready
            foreach (var t in AllComponents)
                MediaCore.Renderers[t]?.OnStarting();

            // Mark as initialized
            HasInitialized.Value = true;
            return true;
        }

        /// <summary>
        /// Ensures the real-time clocks do not lag or move beyond the range of their corresponding blocks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AlignClocksToPlayback()
        {
            // we don't want to disturb the clocks or align it if we are not ready
            if (Commands.HasPendingCommands)
                return;

            if (Timing.IsRunning)
            {
                var offset = Timing.GetOffsetToReference(MediaType.Audio);
                this.LogDebug(Aspects.Timing,
                    $"CLOCK POS: O: {offset.Format()} A: {Timing.GetPosition(MediaType.Audio).Format()} | V: {Timing.GetPosition(MediaType.Video).Format()}");
            }

            if (HasDisconnectedClocks)
            {
                foreach (var t in AllComponents)
                {
                    if (t == MediaType.Subtitle)
                        continue;

                    var compBlocks = Blocks[t];
                    var compPosition = Timing.GetPosition(t);
                    var isClockRunning = Timing.GetIsRunning(t);

                    if (compBlocks.Count <= 0)
                    {
                        MediaCore.PausePlayback(t, false);

                        if (isClockRunning)
                        {
                            this.LogDebug(Aspects.Timing,
                                $"CLOCK PAUSED: {t} clock was paused at {compPosition.Format()} because no decoded {t} content was found");
                        }

                        continue;
                    }

                    // Don't let the RTC lag behind the blocks or move beyond them
                    if (compPosition.Ticks < compBlocks.RangeStartTime.Ticks)
                    {
                        MediaCore.ChangePlaybackPosition(compBlocks.RangeStartTime, t, isClockRunning);

                        if (isClockRunning)
                        {
                            this.LogDebug(Aspects.Timing,
                                $"CLOCK BEHIND: {t} clock was {compPosition.Format()}. It was updated to {compBlocks.RangeStartTime.Format()}");
                        }
                    }
                    else if (compPosition.Ticks > compBlocks.RangeEndTime.Ticks)
                    {
                        if (t != MediaType.Audio)
                            MediaCore.PausePlayback(t, false);

                        MediaCore.ChangePlaybackPosition(compBlocks.RangeEndTime, t, isClockRunning);

                        if (isClockRunning)
                        {
                            this.LogDebug(Aspects.Timing,
                                $"CLOCK AHEAD : {t} clock was {compPosition.Format()}. It was updated to {compBlocks.RangeEndTime.Format()}");
                        }
                    }
                }
            }
            else
            {
                // Get a reference to the main blocks.
                // The range will be 0 if there are no blocks.
                var position = MediaCore.PlaybackPosition;
                var isClockRunning = Timing.IsRunning;

                if (MainBlocks.Count == 0)
                {
                    // We have no main blocks in range. All we can do is pause the clock
                    if (isClockRunning)
                    {
                        this.LogDebug(Aspects.Timing,
                            $"CLOCK PAUSED: playback clock was paused at {position.Format()} because no decoded {MainMediaType} content was found");
                    }

                    MediaCore.PausePlayback();
                    return;
                }

                if (position.Ticks < MainBlocks.RangeStartTime.Ticks)
                {
                    // Don't let the RTC lag behind what is available on the main component
                    MediaCore.ChangePlaybackPosition(MainBlocks.RangeStartTime);
                    this.LogTrace(Aspects.Timing,
                        $"CLOCK BEHIND: playback clock was {position.Format()}. It was updated to {MainBlocks.RangeStartTime.Format()}");
                }
                else if (position.Ticks > MainBlocks.RangeEndTime.Ticks)
                {
                    // Don't let the RTC move beyond what is available on the main component
                    MediaCore.PausePlayback();
                    MediaCore.ChangePlaybackPosition(MainBlocks.RangeEndTime);
                    this.LogTrace(Aspects.Timing,
                        $"CLOCK AHEAD : playback clock was {position.Format()}. It was updated to {MainBlocks.RangeEndTime.Format()}");
                }
            }
        }

        /// <summary>
        /// Speeds up or slows down the speed ratio until the packet buffer
        /// becomes the ideal to continue stable rendering.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CatchUpWithLiveStream()
        {
            // TODO: This is not yet complete.
            // It will fail on m3u8 files for example
            // I am using 2 approches: dealing with timing and dealing with speed ratios
            // I need more time to complete.
            const double DefaultMinBufferMs = 500d;
            const double DefaultMaxBufferMs = 1000d;
            const double UpdateTimeoutMs = 100d;

            if (!State.IsLiveStream)
                return;

            // Check if we have a valid duration
            if (State.PacketBufferDuration == TimeSpan.MinValue)
                return;

            var maxBufferedMs = DefaultMaxBufferMs;
            var minBufferedMs = DefaultMinBufferMs;
            var bufferedMs = Components.Main.BufferDuration.TotalMilliseconds; // State.PacketBufferDuration.TotalMilliseconds;

            if (State.HasAudio && State.HasVideo && !HasDisconnectedClocks)
            {
                var videoStartOffset = Components[MediaType.Video].StartTime;
                var audioStartOffset = Components[MediaType.Audio].StartTime;

                if (videoStartOffset != TimeSpan.MinValue && audioStartOffset != TimeSpan.MinValue)
                {
                    var offsetMs = Math.Abs(videoStartOffset.TotalMilliseconds - audioStartOffset.TotalMilliseconds);
                    maxBufferedMs = Math.Max(maxBufferedMs, offsetMs * 2);
                    minBufferedMs = Math.Min(minBufferedMs, maxBufferedMs / 2d);
                }
            }

            var canChangeSpeed = !MediaCore.IsSyncBuffering && !Commands.HasPendingCommands;
            var needsSpeedUp = canChangeSpeed && bufferedMs > maxBufferedMs;
            var needsSlowDown = canChangeSpeed && bufferedMs < minBufferedMs;
            var needsSpeedChange = needsSpeedUp || needsSlowDown;
            var lastUpdateSinceMs = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - LastSpeedRatioTime.Ticks).TotalMilliseconds;
            var bufferedDelta = needsSpeedUp
                ? bufferedMs - maxBufferedMs
                : minBufferedMs - bufferedMs;

            if (!needsSpeedChange || lastUpdateSinceMs < UpdateTimeoutMs)
                return;

            // TODO: Another option is to mess around some with the timing itself
            // instead of using the speedratio.
            if (bufferedDelta > 100d && (needsSpeedUp || needsSlowDown))
            {
                var deltaPosition = TimeSpan.FromMilliseconds(bufferedDelta / 10);
                if (needsSlowDown) deltaPosition = deltaPosition.Negate();

                Timing.Update(Timing.Position.Add(deltaPosition), MediaType.None);
                LastSpeedRatioTime = DateTime.UtcNow;

                this.LogWarning(nameof(BlockRenderingWorker),
                    $"RT SYNC: Buffered: {bufferedMs:0.000} ms. | Delta: {bufferedDelta:0.000} ms. | Adjustment: {deltaPosition.TotalMilliseconds:0.000} ms.");
            }

            // function computes large changes for large differences.
            /*
            var speedRatioDelta = Math.Min(10d + (Math.Pow(bufferedDelta, 2d) / 100000d), 50d) / 100d;
            if (bufferedDelta < 100d && !needsSlowDown)
                speedRatioDelta = 0d;

            var originalSpeedRatio = State.SpeedRatio;
            var changePercent = (needsSlowDown ? -1d : 1d) * speedRatioDelta;
            State.SpeedRatio = Constants.DefaultSpeedRatio + changePercent;

            if (originalSpeedRatio != State.SpeedRatio)
                LastSpeedRatioTime = DateTime.UtcNow;
            */
        }

        /// <summary>
        /// Enters the sync-buffering scenario if needed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnterSyncBuffering()
        {
            // Determine if Sync-buffering can be potentially entered.
            // Entering the sync-buffering state pauses the RTC and forces the decoder make
            // components catch up with the main component.
            if (MediaCore.IsSyncBuffering || HasDisconnectedClocks || Commands.HasPendingCommands ||
                State.MediaState != MediaPlaybackState.Play || State.HasMediaEnded || Container.IsAtEndOfStream)
            {
                return;
            }

            foreach (var t in AllComponents)
            {
                if (t == MediaType.Subtitle || t == MainMediaType)
                    continue;

                // We don't want to sync-buffer on attached pictures
                if (Components[t].IsStillPictures)
                    continue;

                // If we have data on the t component beyond the start time of the main
                // we don't need to enter sync-buffering.
                if (Blocks[t].RangeEndTime >= MainBlocks.RangeStartTime)
                    continue;

                // If we are not in range of the non-main component we need to
                // enter sync-buffering
                MediaCore.SignalSyncBufferingEntered();
                return;
            }
        }

        /// <summary>
        /// Exits the sync-buffering state.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitSyncBuffering(CancellationToken ct)
        {
            // Don't exit syc-buffering if we are not in syncbuffering
            if (!MediaCore.IsSyncBuffering)
                return;

            // Detect if an exit from Sync Buffering is required
            var canExitSyncBuffering = MainBlocks.Count > 0;
            var mustExitSyncBuffering =
                ct.IsCancellationRequested ||
                MediaCore.HasDecodingEnded ||
                Container.IsAtEndOfStream ||
                State.HasMediaEnded ||
                Commands.HasPendingCommands ||
                HasDisconnectedClocks;

            try
            {
                if (mustExitSyncBuffering)
                {
                    this.LogDebug(Aspects.ReadingWorker, $"SYNC-BUFFER: 'must exit' condition met.");
                    return;
                }

                if (!canExitSyncBuffering)
                    return;

                foreach (var t in AllComponents)
                {
                    if (t == MediaType.Subtitle || t == MainMediaType)
                        continue;

                    // We don't want to consider sync-buffer on attached pictures
                    if (Components[t].IsStillPictures)
                        continue;

                    // If we don't have data on the t component beyond the mid time of the main
                    // we can't exit sync-buffering.
                    if (Blocks[t].RangeEndTime < MainBlocks.RangeMidTime)
                    {
                        canExitSyncBuffering = false;
                        break;
                    }
                }
            }
            finally
            {
                // Exit sync-buffering state if we can or we must
                if (mustExitSyncBuffering || canExitSyncBuffering)
                {
                    AlignClocksToPlayback();
                    MediaCore.SignalSyncBufferingExited();
                }
            }
        }

        /// <summary>
        /// Waits for seek blocks to become available.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WaitForSeekBlocks(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested
            && Commands.IsActivelySeeking
            && !MainBlocks.IsInRange(MediaCore.PlaybackPosition))
            {
                // Check if we finally have seek blocks available
                // if we don't get seek blocks in range and we are not step-seeking,
                // then we simply break out of the loop and render whatever it is we have
                // to create the illussion of smooth seeking. For precision seeking we
                // continue the loop.
                if (Commands.ActiveSeekMode == CommandManager.SeekMode.Normal &&
                    !Commands.WaitForSeekBlocks(1))
                {
                    if (!State.ScrubbingEnabled)
                        continue;
                    else
                        break;
                }
            }
        }

        /// <summary>
        /// Renders the available, non-repeated block.
        /// </summary>
        /// <param name="t">The media type.</param>
        /// <returns>Whether a block was sent to its corresponding renderer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool RenderBlock(MediaType t)
        {
            var result = 0;
            var playbackClock = Timing.GetPosition(t);

            try
            {
                // We don't need non-video blocks if we are seeking
                if (Commands.HasPendingCommands && t != MediaType.Video)
                    return result > 0;

                // Get the audio, video, or subtitle block to render
                var currentBlock = t == MediaType.Subtitle && MediaCore.PreloadedSubtitles != null
                    ? MediaCore.PreloadedSubtitles[playbackClock.Ticks]
                    : Blocks[t][playbackClock.Ticks];

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DetectPlaybackEnded()
        {
            var blocks = Blocks[MainMediaType];
            var playbackEndClock = blocks == null || blocks.Count <= 0
                ? Timing.GetEndTime(MainMediaType) ?? TimeSpan.MaxValue
                : blocks.RangeEndTime;

            // Check End of Media Scenarios
            if (!Commands.HasPendingCommands
                && MediaCore.HasDecodingEnded
                && !CanResumeClock(MainMediaType))
            {
                // Rendered all and nothing else to render
                if (State.HasMediaEnded == false)
                {
                    if (Container.IsStreamSeekable)
                    {
                        var actualComponentDuration = TimeSpan.FromTicks(playbackEndClock.Ticks - Main.StartTime.Ticks);
                        Main.Duration = actualComponentDuration.Ticks > 0
                            ? actualComponentDuration
                            : Main.Duration;
                    }

                    MediaCore.PausePlayback();
                    MediaCore.ChangePlaybackPosition(playbackEndClock);
                }

                State.MediaState = MediaPlaybackState.Stop;
                State.HasMediaEnded = true;
            }
            else
            {
                State.HasMediaEnded = false;
            }
        }

        /// <summary>
        /// Reports the playback position if needed and
        /// resumes the playback clock if the conditions allow for it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReportAndResumePlayback()
        {
            var hasPendingCommands = Commands.HasPendingCommands;
            var isSyncBuffering = MediaCore.IsSyncBuffering;

            // Notify a change in playback position
            if (!hasPendingCommands && !isSyncBuffering)
                State.ReportPlaybackPosition();

            // We don't want to resume the clock if we are not ready for playback
            if (State.MediaState != MediaPlaybackState.Play || isSyncBuffering ||
                hasPendingCommands)
            {
                return;
            }

            // wait for packets
            if (MediaOptions.MinimumPlaybackBufferPercent > 0 &&
                MediaCore.ShouldReadMorePackets &&
                !Components.HasEnoughPackets &&
                State.BufferingProgress < Math.Min(1, MediaOptions.MinimumPlaybackBufferPercent))
            {
                return;
            }

            // Resume individual clock components
            foreach (var t in AllComponents)
            {
                if (!CanResumeClock(t))
                    continue;

                Timing.Play(t);
            }
        }

        /// <summary>
        /// Gets a value indicating whther a component's timing can be resumed.
        /// </summary>
        /// <param name="t">The component media type.</param>
        /// <returns>Whether the clock can be resumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanResumeClock(MediaType t)
        {
            var blocks = Blocks[t == MediaType.None ? Timing.ReferenceType : t];
            if (blocks == null || blocks.Count <= 0)
                return false;

            return Timing.GetPosition(t).Ticks < blocks.RangeEndTime.Ticks;
        }

        /// <summary>
        /// Sends the given block to its corresponding media renderer.
        /// </summary>
        /// <param name="incomingBlock">The block.</param>
        /// <param name="playbackPosition">The clock position.</param>
        /// <returns>
        /// The number of blocks sent to the renderer.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SendBlockToRenderer(MediaBlock incomingBlock, TimeSpan playbackPosition)
        {
            // No blocks were rendered
            if (incomingBlock == null || incomingBlock.IsDisposed) return 0;

            var t = incomingBlock.MediaType;
            var isAttachedPicture = t == MediaType.Video && Components[t].IsStillPictures;
            var currentBlockStartTime = MediaCore.CurrentRenderStartTime.ContainsKey(t)
                ? MediaCore.CurrentRenderStartTime[t]
                : TimeSpan.MinValue;

            var isRepeatedBlock = currentBlockStartTime != TimeSpan.MinValue && currentBlockStartTime == incomingBlock.StartTime;
            var requiresRepeatedBlocks = t == MediaType.Audio || isAttachedPicture;

            // Render by forced signal (TimeSpan.MinValue) or because simply it is time to do so
            // otherwise simply skip block rendering as we have sent the block already.
            if (isRepeatedBlock && !requiresRepeatedBlocks)
                return 0;

            // Process property changes coming from video blocks
            State.UpdateDynamicBlockProperties(incomingBlock);

            // Capture the last render time so we don't repeat the block
            MediaCore.CurrentRenderStartTime[t] = incomingBlock.StartTime;

            // Send the block to its corresponding renderer
            MediaCore.Renderers[t]?.Render(incomingBlock, playbackPosition);

            // Log the block statistics for debugging
            LogRenderBlock(incomingBlock, playbackPosition);

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
            if (!Debugger.IsAttached) return;

            try
            {
                var drift = TimeSpan.FromTicks(clockPosition.Ticks - block.StartTime.Ticks);
                this.LogTrace(Aspects.RenderingWorker,
                    $"{block.MediaType.ToString().Substring(0, 1)} "
                    + $"BLK: {block.StartTime.Format()} | "
                    + $"CLK: {clockPosition.Format()} | "
                    + $"DFT: {drift.TotalMilliseconds,4:0} | "
                    + $"IX: {block.Index,3} | "
                    + $"RNG: {Blocks[block.MediaType].GetRangePercent(clockPosition):p} | "
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
