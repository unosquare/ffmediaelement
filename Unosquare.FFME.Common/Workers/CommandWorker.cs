namespace Unosquare.FFME.Workers
{
    using Core;
    using Decoding;
    using Primitives;
    using Shared;
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class CommandWorker : TimerWorkerBase, IMediaWorker, ILoggingSource
    {
        private readonly ManualResetEventSlim DirectCommandCompleted = new ManualResetEventSlim(true);
        private readonly ManualResetEventSlim PriorityCommandCompleted = new ManualResetEventSlim(true);
        private readonly ManualResetEventSlim SeekCompleted = new ManualResetEventSlim(true);

        private readonly AtomicBoolean m_HasPendingDirectCommands = new AtomicBoolean(false);
        private readonly AtomicInteger m_PendingPriorityCommand = new AtomicInteger(0);

        private readonly AtomicBoolean m_IsOpening = new AtomicBoolean(false);
        private readonly AtomicBoolean m_IsClosing = new AtomicBoolean(false);
        private readonly AtomicBoolean m_IsChanging = new AtomicBoolean(false);
        private readonly AtomicBoolean m_IsSeeking = new AtomicBoolean(false);

        private readonly object SyncLock = new object();

        private SeekOperation QueuedSeekOperation = null;
        private Task<bool> QueuedSeekTask;

        public CommandWorker(MediaEngine mediaCore)
            : base(nameof(CommandWorker), Constants.Interval.HighPriority)
        {
            MediaCore = mediaCore;
        }

        private enum DirectCommand
        {
            Open,
            Close,
            Change
        }

        private enum PriorityCommand
        {
            None,
            Play,
            Pause,
            Stop
        }

        private enum SeekMode
        {
            /// <summary>Normal seek mode</summary>
            Normal,

            /// <summary>Stop seek mode</summary>
            Stop,

            /// <summary>Frame step forward</summary>
            StepForward,

            /// <summary>Frame step backward</summary>
            StepBackward
        }

        public MediaEngine MediaCore { get; }

        public ILoggingHandler LoggingHandler => MediaCore;

        public bool IsOpening
        {
            get => m_IsOpening.Value;
            private set => m_IsOpening.Value = value;
        }

        public bool IsClosing
        {
            get => m_IsClosing.Value;
            private set => m_IsClosing.Value = value;
        }

        public bool IsChanging
        {
            get => m_IsChanging.Value;
            private set => m_IsChanging.Value = value;
        }

        public bool IsSeeking
        {
            get => m_IsSeeking.Value;
            private set => m_IsSeeking.Value = value;
        }

        private MediaEngineState State => MediaCore.State;

        private bool HasPendingDirectCommands
        {
            get => m_HasPendingDirectCommands.Value;
            set => m_HasPendingDirectCommands.Value = value;
        }

        private PriorityCommand PendingPriorityCommand
        {
            get => (PriorityCommand)m_PendingPriorityCommand.Value;
            set => m_PendingPriorityCommand.Value = (int)value;
        }

        private bool CanPlay
        {
            get
            {
                if (MediaCore.State.HasMediaEnded)
                    return false;

                if (State.IsLiveStream)
                    return true;

                if (!State.IsSeekable)
                    return true;

                if (!State.NaturalDuration.HasValue)
                    return true;

                if (State.NaturalDuration == TimeSpan.MinValue)
                    return true;

                return MediaCore.WallClock < State.NaturalDuration;
            }
        }

        #region Public API

        public Task<bool> OpenMediaAsync(Uri uri)
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing || MediaCore.State.IsOpen || HasPendingDirectCommands)
                    return Task.FromResult(false);

                return ExecuteDirectCommand(DirectCommand.Open, () => OpenMedia(null, uri));
            }
        }

        public Task<bool> OpenMediaAsync(IMediaInputStream stream)
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing || MediaCore.State.IsOpen || HasPendingDirectCommands)
                    return Task.FromResult(false);

                return ExecuteDirectCommand(DirectCommand.Open, () => OpenMedia(stream, stream.StreamUri));
            }
        }

        public Task<bool> CloseMediaAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing || !MediaCore.State.IsOpen || HasPendingDirectCommands)
                    return Task.FromResult(false);

                return ExecuteDirectCommand(DirectCommand.Close, () => CloseMedia());
            }
        }

        public Task<bool> ChangeMediaAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing || MediaCore.State.IsOpen == false || HasPendingDirectCommands)
                    return Task.FromResult(false);

                return ExecuteDirectCommand(DirectCommand.Change, () => ChangeMedia(MediaCore.Clock.IsRunning));
            }
        }

        public Task<bool> PlayMediaAsync() => QueuePriorityCommand(PriorityCommand.Play);

        public Task<bool> PauseMediaAsync() => QueuePriorityCommand(PriorityCommand.Pause);

        public Task<bool> StopMediaAsync() => QueuePriorityCommand(PriorityCommand.Stop);

        public Task<bool> SeekMediaAsync(TimeSpan seekTarget) => QueueSeekCommand(seekTarget, SeekMode.Normal);

        public Task<bool> StepForwardAsync() => QueueSeekCommand(TimeSpan.Zero, SeekMode.StepForward);

        public Task<bool> StepBackwardAsync() => QueueSeekCommand(TimeSpan.Zero, SeekMode.StepBackward);

        #endregion

        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            var priorityCommand = PendingPriorityCommand;

            switch (priorityCommand)
            {
                case PriorityCommand.Play:
                    PlayMedia();
                    break;
                case PriorityCommand.Pause:
                    PauseMedia();
                    break;
                case PriorityCommand.Stop:
                    StopMedia();
                    break;
                default:
                    break;
            }

            if (priorityCommand != PriorityCommand.None)
            {
                ClearSeekCommands();
                ClearPriorityCommands();
                return;
            }

            var handledSeek = false;
            while (true)
            {
                SeekOperation seekOperation;
                lock (SyncLock)
                {
                    seekOperation = QueuedSeekOperation;
                    QueuedSeekOperation = null;
                    QueuedSeekTask = null;
                }

                if (seekOperation == null)
                    break;

                handledSeek = true;
                SeekMedia(seekOperation, ct);
            }

            lock (SyncLock)
            {
                IsSeeking = QueuedSeekOperation != null;

                if (handledSeek)
                    MediaCore.Workers.Resume();
            }
        }

        protected override void OnCycleException(Exception ex)
        {
            throw new NotImplementedException();
        }

        private Task<bool> ExecuteDirectCommand(DirectCommand command, Func<bool> commandDeleagte)
        {
            HasPendingDirectCommands = true;
            IsOpening = command == DirectCommand.Open;
            IsClosing = command == DirectCommand.Close;
            IsChanging = command == DirectCommand.Change;

            var commandTask = new Task<bool>(() =>
            {
                var result = false;
                Exception commandException = null;

                // pause the queue processor
                PauseAsync().Wait();

                // clear the command queue and requests
                ClearPriorityCommands();
                ClearSeekCommands();

                // execute the command
                try
                {
                    result = commandDeleagte.Invoke();
                }
                catch (Exception ex)
                {
                    commandException = ex;
                }

                // Update the commanding state
                IsOpening = false;
                IsChanging = false;
                IsClosing = false;

                // Update the sate based on command result
                PostProcessDirectCommand(command, commandException);

                // reset the pending state
                HasPendingDirectCommands = false;

                if (State.IsOpen)
                    ResumeAsync();

                return result;
            });

            commandTask.ConfigureAwait(false);
            commandTask.Start();

            return commandTask;
        }

        private void PostProcessDirectCommand(DirectCommand command, Exception commandException)
        {
            if (command == DirectCommand.Open)
            {
                MediaCore.State.UpdateFixedContainerProperties();

                if (commandException == null)
                {
                    MediaCore.State.UpdateMediaState(PlaybackStatus.Stop);
                    MediaCore.SendOnMediaOpened();
                }
                else
                {
                    MediaCore.ResetPosition();
                    MediaCore.State.UpdateMediaState(PlaybackStatus.Close);
                    MediaCore.SendOnMediaFailed(commandException);
                }

                this.LogDebug(Aspects.EngineCommand, $"{command} Completed");
            }

            // TODO: finish implementing command postprocessing
        }

        private Task<bool> QueuePriorityCommand(PriorityCommand command)
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing || !MediaCore.State.IsOpen || HasPendingDirectCommands || !PriorityCommandCompleted.IsSet)
                    return Task.FromResult(false);

                PendingPriorityCommand = command;
                PriorityCommandCompleted.Reset();

                var commandTask = new Task<bool>(() =>
                {
                    ResumeAsync().Wait();
                    PriorityCommandCompleted.Wait();
                    return true;
                });

                commandTask.ConfigureAwait(false);
                commandTask.Start();

                return commandTask;
            }
        }

        private Task<bool> QueueSeekCommand(TimeSpan seekTarget, SeekMode seekMode)
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing || MediaCore.State.IsOpen == false ||
                    HasPendingDirectCommands || PendingPriorityCommand != PriorityCommand.None)
                    return Task.FromResult(false);

                IsSeeking = true;

                if (QueuedSeekTask != null)
                {
                    QueuedSeekOperation.Mode = seekMode;
                    QueuedSeekOperation.Position = seekTarget;
                    return QueuedSeekTask;
                }

                var seekOperation = new SeekOperation(seekTarget, SeekMode.Normal);
                QueuedSeekOperation = seekOperation;
                QueuedSeekTask = new Task<bool>(() =>
                {
                    seekOperation.Wait();
                    return true;
                });

                QueuedSeekTask.ConfigureAwait(false);
                QueuedSeekTask.Start();

                return QueuedSeekTask;
            }
        }

        private void ClearPriorityCommands()
        {
            lock (SyncLock)
            {
                PendingPriorityCommand = PriorityCommand.None;
                PriorityCommandCompleted.Set();
            }
        }

        private void ClearSeekCommands()
        {
            lock (SyncLock)
            {
                QueuedSeekOperation?.Dispose();
                QueuedSeekOperation = null;
                QueuedSeekTask = null;
                IsSeeking = false;
            }
        }

        private bool OpenMedia(IMediaInputStream inputStream, Uri streamUri)
        {
            // Notify Media will start opening
            this.LogDebug(Aspects.EngineCommand, $"{DirectCommand.Open} Entered");
            var result = false;

            try
            {
                // TODO: Sometimes when the stream can't be read, the sample player stays as if it were trying to open
                // until the interrupt timeout occurs but and the Real-Time Clock continues. Strange behavior. Investigate more.

                // Signal the initial state
                var source = inputStream == null ? streamUri : inputStream.StreamUri;
                State.ResetAll();
                State.UpdateSource(source);

                // Register FFmpeg libraries if not already done
                if (MediaEngine.LoadFFmpeg())
                {
                    // Log an init message
                    this.LogInfo(Aspects.EngineCommand,
                        $"{nameof(FFInterop)}.{nameof(FFInterop.Initialize)}: FFmpeg v{MediaEngine.FFmpegVersionInfo}");
                }

                // Create a default stream container configuration object
                var containerConfig = new ContainerConfiguration();

                // Convert the URI object to something the Media Container understands (Uri to String)
                var mediaUrl = Uri.EscapeUriString(source.ToString());

                // When opening via URL (and not via custom input stream), fix up the protocols and stuff
                if (inputStream == null)
                {
                    try
                    {
                        // the async protocol prefix allows for increased performance for local files.
                        // or anything that is file-system related
                        if (source.IsFile || source.IsUnc)
                        {
                            // Set the default protocol Prefix
                            // containerConfig.ProtocolPrefix = "async";
                            mediaUrl = source.LocalPath;
                        }
                    }
                    catch { /* Ignore exception and continue */ }

                    // Support device URLs
                    // GDI GRAB: Example URI: device://gdigrab?desktop
                    if (string.IsNullOrWhiteSpace(source.Scheme) == false
                        && (source.Scheme.Equals("format") || source.Scheme.Equals("device"))
                        && string.IsNullOrWhiteSpace(source.Host) == false
                        && string.IsNullOrWhiteSpace(containerConfig.ForcedInputFormat)
                        && string.IsNullOrWhiteSpace(source.Query) == false)
                    {
                        // Update the Input format and container input URL
                        // It is also possible to set some input options as follows:
                        // ReSharper disable once CommentTypo
                        // streamOptions.PrivateOptions["framerate"] = "20";
                        containerConfig.ForcedInputFormat = source.Host;
                        mediaUrl = Uri.UnescapeDataString(source.Query).TrimStart('?');
                        this.LogInfo(Aspects.EngineCommand,
                            $"Media URI will be updated. Input Format: {source.Host}, Input Argument: {mediaUrl}");
                    }
                }

                // Allow the stream input options to be changed
                MediaCore.SendOnMediaInitializing(containerConfig, mediaUrl);

                // Instantiate the internal container using either a URL (default) or a custom input stream.
                MediaCore.Container = inputStream == null ?
                    new MediaContainer(mediaUrl, containerConfig, MediaCore) :
                    new MediaContainer(inputStream, containerConfig, MediaCore);

                // Notify the user media is opening and allow for media options to be modified
                // Stuff like audio and video filters and stream selection can be performed here.
                State.UpdateFixedContainerProperties();
                MediaCore.SendOnMediaOpening();

                // Side-load subtitles if requested
                MediaCore.PreLoadSubtitles();

                // Get the main container open
                MediaCore.Container.Open();

                // Reset buffering properties
                State.UpdateFixedContainerProperties();
                State.InitializeBufferingStatistics();

                // Check if we have at least audio or video here
                if (State.HasAudio == false && State.HasVideo == false)
                    throw new MediaContainerException("Unable to initialize at least one audio or video component from the input stream.");

                // Charge! We are good to go, fire up the worker threads!
                MediaCore.StartWorkers();

                // Update the media state
                State.UpdateMediaState(PlaybackStatus.Stop);
                MediaCore.SendOnMediaOpened();

                result = true;
            }
            catch
            {
                try { MediaCore.StopWorkers(); } catch { /* Ignore any exceptions and continue */ }
                try { MediaCore.Container?.Dispose(); } catch { /* Ignore any exceptions and continue */ }
                MediaCore.DisposePreloadedSubtitles();
                MediaCore.Container = null;
                throw;
            }

            return result;
        }

        private bool CloseMedia()
        {
            var result = false;

            try
            {
                this.LogDebug(Aspects.EngineCommand, $"{DirectCommand.Close} Entered");

                // Wait for the workers to stop
                MediaCore.StopWorkers();

                // Dispose the container
                MediaCore.Container?.Dispose();
                MediaCore.Container = null;

                // Dispose the Blocks for all components
                foreach (var kvp in MediaCore.Blocks)
                    kvp.Value.Dispose();

                MediaCore.Blocks.Clear();
                MediaCore.DisposePreloadedSubtitles();

                // Clear the render times
                MediaCore.LastRenderTime.Clear();

                result = true;
            }
            catch
            {
                // ignore
            }
            finally
            {
                // Update notification properties
                State.ResetAll();
                MediaCore.ResetPosition();
                State.UpdateMediaState(PlaybackStatus.Close);
                State.UpdateSource(null);

                // Notify media has closed
                MediaCore.SendOnMediaClosed();
                LogReferenceCounter();
                this.LogDebug(Aspects.EngineCommand, $"{DirectCommand.Close} Completed");
            }

            return result;
        }

        private bool ChangeMedia(bool playWhenCompleted)
        {
            this.LogDebug(Aspects.EngineCommand, $"{DirectCommand.Change} Entered");
            var result = false;

            try
            {
                // Signal the start of a sync-buffering scenario
                MediaCore.Clock.Pause();

                // Wait for the cycles to complete
                MediaCore.Workers.Pause();

                // Signal a change so the user get the chance to update
                // selected streams and options
                MediaCore.SendOnMediaChanging();

                // Side load subtitles
                MediaCore.PreLoadSubtitles();

                // Capture the current media types before components change
                var oldMediaTypes = MediaCore.Container.Components.MediaTypes.ToArray();

                // Recreate selected streams as media components
                var mediaTypes = MediaCore.Container.UpdateComponents();
                MediaCore.State.UpdateFixedContainerProperties();

                // find all existing component blocks and renderers that no longer exist
                // We always remove the audio component in case there is a change in audio device
                var removableMediaTypes = oldMediaTypes
                    .Where(t => mediaTypes.Contains(t) == false)
                    .Union(new[] { MediaType.Audio })
                    .Distinct()
                    .ToArray();

                // find all existing component blocks and renderers that no longer exist
                foreach (var t in removableMediaTypes)
                {
                    // Remove the renderer for the component
                    if (MediaCore.Renderers.ContainsKey(t))
                    {
                        MediaCore.Renderers[t].Close();
                        MediaCore.Renderers.Remove(t);
                    }

                    // Remove the block buffer for the component
                    if (!MediaCore.Blocks.ContainsKey(t)) continue;
                    MediaCore.Blocks[t]?.Dispose();
                    MediaCore.Blocks.Remove(t);
                }

                // Create the block buffers and renderers as necessary
                foreach (var t in mediaTypes)
                {
                    if (MediaCore.Blocks.ContainsKey(t) == false)
                        MediaCore.Blocks[t] = new MediaBlockBuffer(Constants.MaxBlocks[t], t);

                    if (MediaCore.Renderers.ContainsKey(t) == false)
                        MediaCore.Renderers[t] = MediaEngine.Platform.CreateRenderer(t, MediaCore);

                    MediaCore.Blocks[t].Clear();
                    MediaCore.Renderers[t].WaitForReadyState();
                }

                // Depending on whether or not the media is seekable
                // perform either a seek operation or a quick buffering operation.
                if (State.IsSeekable)
                {
                    // Let's simply do an automated seek
                    SeekMedia(new SeekOperation(MediaCore.WallClock, SeekMode.Normal), CancellationToken.None);
                }
                else
                {
                    // Let's perform quick-buffering
                    MediaCore.Container.Components.RunQuickBuffering(MediaCore);

                    // Mark the renderers as invalidated
                    foreach (var t in mediaTypes)
                        MediaCore.InvalidateRenderer(t);
                }

                MediaCore.SendOnMediaChanged();

                if (playWhenCompleted)
                    MediaCore.Clock.Play();

                MediaCore.State.UpdateMediaState(
                    MediaCore.Clock.IsRunning ? PlaybackStatus.Play : PlaybackStatus.Pause);

                result = true;
            }
            catch (Exception ex)
            {
                MediaCore.SendOnMediaFailed(ex);
                MediaCore.State.UpdateMediaState(PlaybackStatus.Pause);
            }
            finally
            {
                this.LogDebug(Aspects.EngineCommand, $"{DirectCommand.Change} Completed");
            }

            return result;
        }

        private bool PlayMedia()
        {
            if (!CanPlay)
                return false;

            foreach (var renderer in MediaCore.Renderers.Values)
                renderer.Play();

            MediaCore.ResumePlayback();

            return true;
        }

        private bool PauseMedia()
        {
            if (State.CanPause == false)
                return false;

            MediaCore.Clock.Pause();

            foreach (var renderer in MediaCore.Renderers.Values)
                renderer.Pause();

            MediaCore.ChangePosition(MediaCore.SnapPositionToBlockPosition(MediaCore.WallClock));
            State.UpdateMediaState(PlaybackStatus.Pause);
            return true;
        }

        private bool StopMedia()
        {
            MediaCore.Clock.Reset();
            SeekMedia(new SeekOperation(TimeSpan.Zero, SeekMode.Stop), CancellationToken.None);

            foreach (var renderer in MediaCore.Renderers.Values)
                renderer.Stop();

            State.UpdateMediaState(PlaybackStatus.Stop);
            return true;
        }

        private bool SeekMedia(SeekOperation seekOperation, CancellationToken ct)
        {
            var result = false;
            MediaCore.Clock.Pause();
            var initialPosition = MediaCore.WallClock;
            var hasDecoderSeeked = false;
            var startTime = DateTime.UtcNow;
            var targetSeekMode = seekOperation.Mode;
            var targetPosition = seekOperation.Position;

            try
            {
                var main = MediaCore.Container.Components.MainMediaType;
                var all = MediaCore.Container.Components.MediaTypes;
                var mainBlocks = MediaCore.Blocks[main];

                if (targetSeekMode == SeekMode.StepBackward || targetSeekMode == SeekMode.StepForward)
                {
                    var neighbors = mainBlocks.Neighbors(initialPosition);
                    targetPosition = neighbors[targetSeekMode == SeekMode.StepBackward ? 0 : 1]?.StartTime ??
                        TimeSpan.FromTicks(neighbors[2].StartTime.Ticks - Convert.ToInt64(neighbors[2].Duration.Ticks / 2d));
                }
                else if (targetSeekMode == SeekMode.Stop)
                {
                    targetPosition = TimeSpan.Zero;
                }

                // Check if we already have the block. If we do, simply set the clock position to the target position
                // we don't need anything else. This implements frame-by frame seeking and we need to snap to a discrete
                // position of the main component so it sticks on it.
                if (mainBlocks.IsInRange(targetPosition))
                {
                    MediaCore.ChangePosition(targetPosition);
                    return true;
                }

                // Mark for debugger output
                hasDecoderSeeked = true;

                // wait for the current reading and decoding cycles
                // to finish. We don't want to interfere with reading in progress
                // or decoding in progress. For decoding we already know we are not
                // in a cycle because the decoding worker called this logic.
                MediaCore.Workers.Reading.PauseAsync().Wait();
                MediaCore.Workers.Decoding.PauseAsync().Wait();

                // Signal the starting state clearing the packet buffer cache
                MediaCore.Container.Components.ClearQueuedPackets(flushBuffers: true);

                // Capture seek target adjustment
                var adjustedSeekTarget = targetPosition;
                if (targetPosition != TimeSpan.Zero && mainBlocks.IsMonotonic)
                {
                    var targetSkewTicks = Convert.ToInt64(
                        mainBlocks.MonotonicDuration.Ticks * (mainBlocks.Capacity / 2d));

                    if (adjustedSeekTarget.Ticks >= targetSkewTicks)
                        adjustedSeekTarget = TimeSpan.FromTicks(adjustedSeekTarget.Ticks - targetSkewTicks);
                }

                // Populate frame queues with after-seek operation
                var firstFrame = MediaCore.Container.Seek(adjustedSeekTarget);
                if (firstFrame != null)
                {
                    // Ensure we signal media has not ended
                    State.UpdateMediaEnded(false, TimeSpan.Zero);

                    // Clear Blocks and frames, reset the render times
                    foreach (var mt in all)
                    {
                        MediaCore.Blocks[mt].Clear();
                        MediaCore.InvalidateRenderer(mt);
                    }

                    // Create the blocks from the obtained seek frames
                    MediaCore.Blocks[firstFrame.MediaType]?.Add(firstFrame, MediaCore.Container);

                    // Decode all available queued packets into the media component blocks
                    foreach (var mt in all)
                    {
                        while (MediaCore.Blocks[mt].IsFull == false)
                        {
                            var frame = MediaCore.Container.Components[mt].ReceiveNextFrame();
                            if (frame == null) break;
                            MediaCore.Blocks[mt].Add(frame, MediaCore.Container);
                        }
                    }

                    // Align to the exact requested position on the main component
                    while (MediaCore.ShouldReadMorePackets)
                    {
                        // Check if we are already in range
                        if (mainBlocks.IsInRange(targetPosition)) break;

                        // Read the next packet
                        var packetType = MediaCore.Container.Read();
                        var blocks = MediaCore.Blocks[packetType];
                        if (blocks == null) continue;

                        // Get the next frame
                        if (blocks.RangeEndTime.Ticks < targetPosition.Ticks || blocks.IsFull == false)
                            blocks.Add(MediaCore.Container.Components[packetType].ReceiveNextFrame(), MediaCore.Container);
                    }
                }

                // Find out what the final, best-effort position was
                TimeSpan resultPosition;
                if (mainBlocks.IsInRange(targetPosition) == false)
                {
                    // We don't have a a valid main range
                    var minStartTimeTicks = mainBlocks.RangeStartTime.Ticks;
                    var maxStartTimeTicks = mainBlocks.RangeEndTime.Ticks;

                    this.LogWarning(Aspects.EngineCommand,
                        $"SEEK TP: Target Pos {targetPosition.Format()} not between {mainBlocks.RangeStartTime.TotalSeconds:0.000} " +
                        $"and {mainBlocks.RangeEndTime.TotalSeconds:0.000}");

                    resultPosition = TimeSpan.FromTicks(targetPosition.Ticks.Clamp(minStartTimeTicks, maxStartTimeTicks));
                }
                else
                {
                    resultPosition = mainBlocks.Count == 0 && targetPosition != TimeSpan.Zero ?
                        initialPosition : // Unsuccessful. This initial position is simply what the clock was :(
                        targetPosition; // Successful seek with main blocks in range
                }

                // Write a new Real-time clock position now.
                MediaCore.ChangePosition(resultPosition);
            }
            catch (Exception ex)
            {
                // Log the exception
                this.LogError(Aspects.EngineCommand, "SEEK ERROR", ex);
            }
            finally
            {
                if (hasDecoderSeeked)
                {
                    this.LogTrace(Aspects.EngineCommand,
                        $"SEEK D: Elapsed: {startTime.FormatElapsed()} | Target: {targetPosition.Format()}");
                }

                seekOperation.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Outputs Reference Counter Results
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogReferenceCounter()
        {
            if (MediaEngine.Platform?.IsInDebugMode ?? true) return;
            if (RC.Current.InstancesByLocation.Count <= 0) return;

            var builder = new StringBuilder();
            builder.AppendLine("Unmanaged references were left alive. This is an indication that there is a memory leak.");
            foreach (var kvp in RC.Current.InstancesByLocation)
                builder.AppendLine($"    {kvp.Key,30} - Instances: {kvp.Value}");

            this.LogError(Aspects.ReferenceCounter, builder.ToString());
        }

        private sealed class SeekOperation : IDisposable
        {
            private readonly object SyncLock = new object();
            private bool IsDisposed = false;

            public SeekOperation(TimeSpan position, SeekMode mode)
            {
                Position = position;
                Mode = mode;
            }

            public TimeSpan Position { get; set; }

            public SeekMode Mode { get; set; }

            private ManualResetEventSlim SeekCompleted { get; } = new ManualResetEventSlim(false);

            public void Wait()
            {
                lock (SyncLock)
                {
                    if (IsDisposed) return;
                }

                SeekCompleted.Wait();
            }

            public void Dispose() => Dispose(true);

            private void Dispose(bool alsoManaged)
            {
                lock (SyncLock)
                {
                    if (IsDisposed) return;
                    IsDisposed = true;
                }

                SeekCompleted.Set();
                SeekCompleted.Dispose();
            }
        }
    }
}
