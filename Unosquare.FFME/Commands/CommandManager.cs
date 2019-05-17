namespace Unosquare.FFME.Commands
{
    using Common;
    using Diagnostics;
    using Engine;
    using Primitives;
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides the MediEngine with an API to execute media control commands.
    /// Direct Commands execute immediately (Open, CLose, Change)
    /// Priority Commands execute in the queue but before anything else and are exclusive (Play, Pause, Stop)
    /// Seek commands are queued and replaced. These are processed in a deferred manner by this worker.
    /// </summary>
    /// <seealso cref="TimerWorkerBase" />
    /// <seealso cref="IMediaWorker" />
    /// <seealso cref="ILoggingSource" />
    internal sealed partial class CommandManager : TimerWorkerBase, IMediaWorker, ILoggingSource
    {
        private readonly object SyncLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandManager"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public CommandManager(MediaEngine mediaCore)
            : base(nameof(CommandManager), DefaultPeriod)
        {
            MediaCore = mediaCore;
        }

        #region Properties

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <summary>
        /// Gets a value indicating whether the command manager is executing commands or has pending commands.
        /// </summary>
        public bool HasPendingCommands => IsSeeking || IsDirectCommandPending || IsPriorityCommandPending;

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => MediaCore;

        /// <summary>
        /// Gets the media engine state.
        /// </summary>
        private MediaEngineState State => MediaCore.State;

        /// <summary>
        /// Gets the pending priority command. There can only be one at a time.
        /// </summary>
        private PriorityCommandType PendingPriorityCommand
        {
            get => (PriorityCommandType)m_PendingPriorityCommand.Value;
            set => m_PendingPriorityCommand.Value = (int)value;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Opens the media using a standard URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>An awaitable task which contains a boolean whether or not to resume media when completed.</returns>
        public Task<bool> OpenMediaAsync(Uri uri) => ExecuteDirectCommand(DirectCommandType.Open, () => CommandOpenMedia(null, uri));

        /// <summary>
        /// Opens the media using a custom stream.
        /// </summary>
        /// <param name="stream">The custom input stream.</param>
        /// <returns>An awaitable task which contains a boolean whether or not to resume media when completed.</returns>
        public Task<bool> OpenMediaAsync(IMediaInputStream stream) => ExecuteDirectCommand(DirectCommandType.Open, () => CommandOpenMedia(stream, null));

        /// <summary>
        /// Closes the currently open media.
        /// </summary>
        /// <returns>An awaitable task which contains a boolean whether or not to resume media when completed.</returns>
        public Task<bool> CloseMediaAsync()
        {
            lock (SyncLock)
            {
                if (IsCloseInterruptPending)
                {
                    this.LogWarning(Aspects.EngineCommand, $"Direct Command interrupt for {PendingDirectCommand} is already pending completion.");
                    return Task.FromResult(false);
                }

                var shouldInterrupt =
                    !IsCloseInterruptPending &&
                    PendingDirectCommand != DirectCommandType.Close &&
                    PendingDirectCommand != DirectCommandType.None;

                if (shouldInterrupt)
                {
                    IsCloseInterruptPending = true;
                    MediaCore.Container?.SignalAbortReads(false);

                    return Task.Run(() =>
                    {
                        try
                        {
                            while (HasDirectCommandCompleted == false)
                            {
                                MediaCore.Container?.SignalAbortReads(false);
                                Task.Delay(DefaultPeriod).GetAwaiter().GetResult();
                            }

                            CommandCloseMedia();
                        }
                        catch (Exception ex)
                        {
                            this.LogWarning(Aspects.Container, $"Closing media via interrupt did not execute cleanly. {ex.Message}");
                        }
                        finally
                        {
                            IsCloseInterruptPending = false;
                            PostProcessDirectCommand(DirectCommandType.Close, null, false);
                        }

                        return true;
                    });
                }

                return ExecuteDirectCommand(DirectCommandType.Close, () => CommandCloseMedia());
            }
        }

        /// <summary>
        /// Changes the media components and applies new configuration.
        /// </summary>
        /// <returns>An awaitable task which contains a boolean whether or not to resume media when completed.</returns>
        public Task<bool> ChangeMediaAsync() => ExecuteDirectCommand(DirectCommandType.Change, () => CommandChangeMedia(State.MediaState == MediaPlaybackState.Play));

        /// <summary>
        /// Plays the currently open media.
        /// </summary>
        /// <returns>An awaitable task which contains a boolean result. True means success. False means failere.</returns>
        public Task<bool> PlayMediaAsync() => QueuePriorityCommand(PriorityCommandType.Play);

        /// <summary>
        /// Pauses the currently open media asynchronous.
        /// </summary>
        /// <returns>An awaitable task which contains a boolean result. True means success. False means failere.</returns>
        public Task<bool> PauseMediaAsync() => QueuePriorityCommand(PriorityCommandType.Pause);

        /// <summary>
        /// Stops the currently open media. This seeks to the start of the input and pauses the clock.
        /// </summary>
        /// <returns>An awaitable task which contains a boolean result. True means success. False means failere.</returns>
        public Task<bool> StopMediaAsync() => QueuePriorityCommand(PriorityCommandType.Stop);

        /// <summary>
        /// Queues a seek operation.
        /// </summary>
        /// <param name="seekTarget">The seek target.</param>
        /// <returns>An awaitable task which contains a boolean result. True means success. False means failere.</returns>
        public Task<bool> SeekMediaAsync(TimeSpan seekTarget) => QueueSeekCommand(seekTarget, SeekMode.Normal);

        /// <summary>
        /// Queues a seek operation that steps a single frame forward.
        /// </summary>
        /// <returns>An awaitable task which contains a boolean result. True means success. False means failere.</returns>
        public Task<bool> StepForwardAsync() => QueueSeekCommand(TimeSpan.Zero, SeekMode.StepForward);

        /// <summary>
        /// Queues a seek operation that steps a single frame backward.
        /// </summary>
        /// <returns>An awaitable task which contains a boolean result. True means success. False means failere.</returns>
        public Task<bool> StepBackwardAsync() => QueueSeekCommand(TimeSpan.Zero, SeekMode.StepBackward);

        /// <summary>
        /// When a seek operation is in progress, this method blocks until the first block of the main
        /// component is available.
        /// </summary>
        /// <param name="millisecondsTimeout">The timeout to wait for.</param>
        /// <returns>If the wait completed successully.</returns>
        public bool WaitForSeekBlocks(int millisecondsTimeout) => SeekBlocksAvailable.Wait(millisecondsTimeout);

        #endregion

        #region Worker Implementation

        /// <inheritdoc />
        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            // Before anything, let's process priority commands
            var priorityCommand = PendingPriorityCommand;
            if (priorityCommand != PriorityCommandType.None)
            {
                // Pause all the workers in preparation for execution
                MediaCore.Workers.PauseAll();

                // Execute the pending priority command
                if (priorityCommand == PriorityCommandType.Play)
                    CommandPlayMedia();
                else if (priorityCommand == PriorityCommandType.Pause)
                    CommandPauseMedia();
                else if (priorityCommand == PriorityCommandType.Stop)
                    CommandStopMedia();
                else
                    throw new NotSupportedException($"Command '{priorityCommand}' is not supported");

                // Finish the command execution
                ClearSeekCommands();
                ClearPriorityCommands();
                MediaCore.Workers.ResumeAll();
                return;
            }

            // Perform current and queued seeks.
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

                ActiveSeekMode = seekOperation.Mode;
                SeekMedia(seekOperation, ct);
            }

            // Handle the case when there is no more seeking needed.
            lock (SyncLock)
            {
                if (IsSeeking && QueuedSeekOperation == null)
                {
                    IsSeeking = false;

                    // Resume the workers since the seek media operation
                    // might have required pausing them.
                    State.ReportPlaybackPosition();
                    MediaCore.Workers.ResumePaused();

                    // Resume if requested
                    if (PlayAfterSeek == true)
                    {
                        PlayAfterSeek = false;
                        State.UpdateMediaState(MediaPlaybackState.Play);
                    }
                    else
                    {
                        if (State.MediaState != MediaPlaybackState.Stop)
                            State.UpdateMediaState(MediaPlaybackState.Pause);
                    }

                    MediaCore.SendOnSeekingEnded();
                }
            }
        }

        /// <inheritdoc />
        protected override void OnCycleException(Exception ex) =>
            this.LogError(Aspects.EngineCommand, "Command Manager Exception Thrown", ex);

        /// <inheritdoc />
        protected override void OnDisposing()
        {
            this.LogDebug(Aspects.EngineCommand, "Dispose Entered. Waiting for Command Manager processor to stop.");
            ClearPriorityCommands();
            ClearSeekCommands();
            SeekBlocksAvailable.Set();

            // wait for any pending direct commands (unlikely)
            this.LogDebug(Aspects.EngineCommand, "Dispose is waiting for pending direct commands.");
            while (IsDirectCommandPending)
                Task.Delay(15).Wait();

            this.LogDebug(Aspects.EngineCommand, "Dispose is closing media.");
            try
            {
                // Execute the close media logic directly
                CommandCloseMedia();
                PostProcessDirectCommand(DirectCommandType.Close, null, false);
            }
            catch (Exception ex)
            {
                this.LogError(Aspects.EngineCommand, "Dispose had issues closing media. This is most likely a bug.", ex);
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool alsoManaged)
        {
            // Call the base dispose method
            base.Dispose(alsoManaged);

            // Dispose unmanged resources
            PriorityCommandCompleted.Dispose();
            SeekBlocksAvailable.Dispose();
            QueuedSeekOperation?.Dispose();
            QueuedSeekOperation = null;
            this.LogDebug(Aspects.EngineCommand, "Dispose completed.");
        }

        #endregion

        /// <summary>
        /// Outputs Reference Counter Results.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogReferenceCounter()
        {
            if (!Debugger.IsAttached) return;
            if (RC.Current.InstancesByLocation.Count <= 0) return;

            var builder = new StringBuilder();
            builder.AppendLine("Unmanaged references were left alive. This is an indication that there is a memory leak.");
            foreach (var kvp in RC.Current.InstancesByLocation)
                builder.AppendLine($"    {kvp.Key,30} - Instances: {kvp.Value}");

            this.LogError(Aspects.ReferenceCounter, builder.ToString());
        }
    }
}
