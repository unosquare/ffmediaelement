namespace Unosquare.FFME.Commands
{
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    /// <inheritdoc />
    /// <summary>
    /// Provides centralized access to playback controls
    /// </summary>
    /// <seealso cref="IDisposable" />
    internal sealed class CommandManager : IDisposable, ILoggingSource
    {
        #region Private Members

        private readonly MediaEngine MediaCore;
        private readonly List<CommandBase> CommandQueue = new List<CommandBase>(32);
        private readonly IWaitEvent DirectCommandEvent = WaitEventFactory.Create(isCompleted: true, useSlim: true);
        private readonly AtomicBoolean m_IsStopWorkersPending = new AtomicBoolean(false);
        private readonly AtomicBoolean PlayAfterSeek = new AtomicBoolean(false);

        private readonly object DirectLock = new object();
        private readonly object QueueLock = new object();
        private readonly object StatusLock = new object();
        private readonly object DisposeLock = new object();

        private readonly AtomicInteger PendingSeekCount = new AtomicInteger(0);
        private readonly AtomicBoolean HasSeekingStarted = new AtomicBoolean(false);
        private readonly IWaitEvent SeekingCommandEvent = WaitEventFactory.Create(isCompleted: true, useSlim: true);

        private bool m_IsClosing;
        private bool m_IsOpening;
        private bool m_IsChanging;
        private bool m_IsDisposed;

        private DirectCommandBase CurrentDirectCommand;
        private CommandBase ExecutingQueueCommand;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandManager" /> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public CommandManager(MediaEngine mediaCore)
        {
            MediaCore = mediaCore;
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => MediaCore;

        /// <summary>
        /// Gets a value indicating whether a direct command is currently executing.
        /// </summary>
        public bool IsExecutingDirectCommand => DirectCommandEvent.IsInProgress;

        /// <summary>
        /// Gets a value indicating whether a close command is in progress
        /// </summary>
        public bool IsClosing
        {
            get { lock (StatusLock) return m_IsClosing; }
        }

        /// <summary>
        /// Gets a value indicating whether an open command is in progress
        /// </summary>
        public bool IsOpening
        {
            get { lock (StatusLock) return m_IsOpening; }
        }

        /// <summary>
        /// Gets a value indicating whether a change media command is in progress
        /// </summary>
        public bool IsChanging
        {
            get { lock (StatusLock) return m_IsChanging; }
        }

        /// <summary>
        /// Gets a value indicating whether the media seeking is in progress.
        /// </summary>
        public bool IsSeeking => IsActivelySeeking || HasQueuedSeekCommands;

        /// <summary>
        /// Gets a value indicating whether a seek command is currently executing.
        /// This differs from the <see cref="IsSeeking"/> property as this is the realtime
        /// state of a seek operation as opposed to a general, delayed state of the command manager.
        /// </summary>
        public bool IsActivelySeeking => SeekingCommandEvent.IsInProgress;

        /// <summary>
        /// Gets a value indicating whether Reading, Decoding and Rendering workers are
        /// pending stop.
        /// </summary>
        public bool IsStopWorkersPending
        {
            get => m_IsStopWorkersPending.Value;
            set => m_IsStopWorkersPending.Value = value;
        }

        /// <summary>
        /// Gets a value indicating whether the command queued contains commands.
        /// </summary>
        public bool HasQueuedCommands
        {
            get { lock (QueueLock) return CommandQueue.Count > 0; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance can execute queued commands.
        /// </summary>
        public bool CanExecuteQueuedCommands
        {
            get
            {
                lock (StatusLock)
                {
                    // If we are closing or already disposed, we can't enqueue
                    if (m_IsClosing || IsDisposed) return false;

                    // If we are not opening or have already opened, we can't enqueue commands
                    return m_IsOpening || MediaCore.State.IsOpen;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        public bool IsDisposed { get { lock (DisposeLock) return m_IsDisposed; } }

        /// <summary>
        /// Gets a value indicating whether the command queue contains seek commands.
        /// </summary>
        private bool HasQueuedSeekCommands
        {
            get { lock (QueueLock) return PendingSeekCount > 0; }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Opens a media URI
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> OpenAsync(Uri uri)
        {
            if (MediaCore.State.IsOpen || IsDisposed || IsExecutingDirectCommand)
                return false;

            return await ExecuteDirectCommand(new DirectOpenCommand(MediaCore, uri)).ConfigureAwait(false);
        }

        /// <summary>
        /// Opens the media stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> OpenAsync(IMediaInputStream stream)
        {
            if (MediaCore.State.IsOpen || IsDisposed || IsExecutingDirectCommand)
                return false;

            return await ExecuteDirectCommand(new DirectOpenCommand(MediaCore, stream)).ConfigureAwait(false);
        }

        /// <summary>
        /// Closes the currently open media.
        /// </summary>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> CloseAsync()
        {
            if (MediaCore.State.IsOpen == false || IsDisposed)
                return false;

            var currentCommand = GetCurrentDirectCommand(CommandType.Close);

            if (currentCommand != null)
                return await currentCommand.Awaiter;

            return !IsExecutingDirectCommand &&
                   await ExecuteDirectCommand(new DirectCloseCommand(MediaCore)).ConfigureAwait(false);
        }

        /// <summary>
        /// Begins the process of changing/updating media parameters.
        /// </summary>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> ChangeMediaAsync()
        {
            if (MediaCore.State.IsOpen == false || IsDisposed)
                return false;

            var currentCommand = GetCurrentDirectCommand(CommandType.ChangeMedia);

            if (currentCommand != null)
                return await currentCommand.Awaiter;

            return !IsExecutingDirectCommand &&
                   await ExecuteDirectCommand(new DirectChangeCommand(MediaCore)).ConfigureAwait(false);
        }

        /// <summary>
        /// Begins playback of the media.
        /// </summary>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> PlayAsync() =>
            await ExecutePriorityCommand(CommandType.Play).ConfigureAwait(false);

        /// <summary>
        /// Pauses the playback of the media.
        /// </summary>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> PauseAsync() =>
            await ExecutePriorityCommand(CommandType.Pause).ConfigureAwait(false);

        /// <summary>
        /// Stops the playback of the media.
        /// </summary>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> StopAsync()
        {
            IncrementPendingSeeks();
            return await ExecutePriorityCommand(CommandType.Stop).ConfigureAwait(false);
        }

        /// <summary>
        /// Seeks to the target position on the media
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> SeekAsync(TimeSpan target)
        {
            IncrementPendingSeeks();
            return await ExecuteDelayedSeekCommand(
                target, SeekCommand.SeekMode.Normal).ConfigureAwait(false);
        }

        /// <summary>
        /// Seeks a single frame forward.
        /// </summary>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> StepForwardAsync()
        {
            IncrementPendingSeeks();
            return await ExecuteDelayedSeekCommand(
                TimeSpan.Zero, SeekCommand.SeekMode.StepForward).ConfigureAwait(false);
        }

        /// <summary>
        /// Seeks a single frame backward.
        /// </summary>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> StepBackwardAsync()
        {
            IncrementPendingSeeks();
            return await ExecuteDelayedSeekCommand(
                TimeSpan.Zero, SeekCommand.SeekMode.StepBackward).ConfigureAwait(false);
        }

        /// <summary>
        /// Waits for any current direct command to finish execution.
        /// </summary>
        public void WaitForDirectCommand() =>
            DirectCommandEvent.Wait();

        /// <summary>
        /// Waits for an active seek command (if any) to complete.
        /// </summary>
        public void WaitForActiveSeekCommand() =>
            SeekingCommandEvent.Wait();

        /// <summary>
        /// Executes the next command in the queued.
        /// </summary>
        /// <returns>The type of command that was executed</returns>
        public CommandType ExecuteNextQueuedCommand()
        {
            if (DirectCommandEvent.IsInProgress)
                return CommandType.None;

            CommandBase command = null;
            lock (QueueLock)
            {
                if (CommandQueue.Count > 0)
                {
                    command = CommandQueue[0];
                    CommandQueue.RemoveAt(0);
                    ExecutingQueueCommand = command;
                }
            }

            if (command == null)
                return CommandType.None;

            try
            {
                // Initiate a seek cycle
                if (command.AffectsSeekingState)
                {
                    SeekingCommandEvent.Begin();
                    MediaCore.State.UpdateMediaState(PlaybackStatus.Manual);
                    if (HasSeekingStarted == false)
                    {
                        HasSeekingStarted.Value = true;
                        PlayAfterSeek.Value = MediaCore.Clock.IsRunning && command is SeekCommand seekCommand &&
                             seekCommand.TargetSeekMode == SeekCommand.SeekMode.Normal;

                        MediaCore.SendOnSeekingStarted();
                    }
                }

                // Execute the command synchronously
                command.Execute();
            }
            finally
            {
                lock (QueueLock)
                {
                    if (command.AffectsSeekingState)
                    {
                        SeekingCommandEvent.Complete();
                        DecrementPendingSeeks();
                        MediaCore.Workers.Resume();
                    }

                    ExecutingQueueCommand = null;
                }
            }

            return command.CommandType;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (DisposeLock)
            {
                if (m_IsDisposed) return;

                // Immediately mark as disposed so no more
                // Commands can be executed/queued
                m_IsDisposed = true;

                // Signal the workers we need to quit
                ClearCommandQueue();
                IsStopWorkersPending = true;
                MediaCore.Container?.SignalAbortReads(false);

                // Wait for any pending direct command
                DirectCommandEvent.Wait();

                // Run the close command directly
                var closeCommand = new DirectCloseCommand(MediaCore);
                closeCommand.Execute();

                // Dispose of additional resources.
                DirectCommandEvent?.Dispose();
                SeekingCommandEvent?.Dispose();
            }
        }

        #endregion

        #region Private Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecrementPendingSeeks()
        {
            lock (QueueLock)
            {
                PendingSeekCount.Value--;
                if (PendingSeekCount > 0) return;

                // Reset the pending Seek Count
                PendingSeekCount.Value = 0;

                // Notify the end of a seek if we previously notified the
                // the start of one.
                if (HasSeekingStarted == false) return;

                // Reset the notification
                HasSeekingStarted.Value = false;

                // Notify seeking has ended
                MediaCore.SendOnSeekingEnded();

                // Resume if requested
                if (PlayAfterSeek == true)
                {
                    PlayAfterSeek.Value = false;
                    MediaCore.ResumePlayback();
                }
                else
                {
                    if (MediaCore.State.MediaState != PlaybackStatus.Stop)
                        MediaCore.State.UpdateMediaState(PlaybackStatus.Pause);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementPendingSeeks() { lock (QueueLock) PendingSeekCount.Value++; }

        /// <summary>
        /// Clears the command queue.
        /// All commands are signaled so all awaiter handles stop awaiting.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearCommandQueue()
        {
            lock (QueueLock)
            {
                for (var i = CommandQueue.Count - 1; i >= 0; i--)
                {
                    var command = CommandQueue[i];
                    command.Dispose();
                    this.LogTrace(Aspects.EngineCommand, $"{nameof(ClearCommandQueue)} - Command Disposed: {command.CommandType}");

                    if (command.AffectsSeekingState)
                        DecrementPendingSeeks();
                }

                CommandQueue.Clear();
            }
        }

        /// <summary>
        /// Gets the current direct command of the specified type.
        /// If there is no direct command executing or the command type does not match
        /// what is currently executed, null is returned.
        /// </summary>
        /// <param name="commandType">Type of the command.</param>
        /// <returns>The currently executing command</returns>
        private CommandBase GetCurrentDirectCommand(CommandType commandType)
        {
            var currentCommand = default(CommandBase);
            lock (DirectLock)
            {
                if (CurrentDirectCommand != null &&
                    CurrentDirectCommand.CommandType == commandType)
                {
                    currentCommand = CurrentDirectCommand;
                }
            }

            return currentCommand;
        }

        /// <summary>
        /// Executes the specified direct command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The awaitable task handle</returns>
        private async Task<bool> ExecuteDirectCommand(DirectCommandBase command)
        {
            if (TryEnterDirectCommand(command) == false)
                return false;

            PrepareForDirectCommand(command.CommandType);
            command.BeginExecute();

            try { return await command.Awaiter; }
            finally { FinalizeDirectCommand(command); }
        }

        /// <summary>
        /// Executes the specified priority command.
        /// </summary>
        /// <param name="commandType">Type of the command.</param>
        /// <returns>The awaitable task handle</returns>
        private async Task<bool> ExecutePriorityCommand(CommandType commandType)
        {
            if (CanExecuteQueuedCommands == false)
            {
                if (CommandBase.TypeAffectsSeekingState(commandType))
                    DecrementPendingSeeks();

                return false;
            }

            CommandBase currentCommand = null;
            lock (QueueLock)
            {
                if (ExecutingQueueCommand != null &&
                    ExecutingQueueCommand.CommandType == commandType)
                {
                    currentCommand = ExecutingQueueCommand;
                }

                if (currentCommand == null)
                {
                    var queuedCommand = CommandQueue
                        .FirstOrDefault(c => c.CommandType == commandType);

                    if (queuedCommand != null)
                        currentCommand = queuedCommand;
                }
            }

            if (currentCommand != null)
            {
                DecrementPendingSeeks();
                return await currentCommand.Awaiter;
            }

            CommandBase command;

            if (commandType == CommandType.Play)
                command = new PlayCommand(MediaCore);
            else if (commandType == CommandType.Pause)
                command = new PauseCommand(MediaCore);
            else if (commandType == CommandType.Stop)
                command = new StopCommand(MediaCore);
            else
                throw new ArgumentException($"{nameof(commandType)} is of invalid type '{commandType}'");

            // Priority commands clear the queue and add themselves.
            lock (QueueLock)
            {
                ClearCommandQueue();
                CommandQueue.Add(command);
                this.LogTrace(Aspects.EngineCommand, $"{nameof(ExecutePriorityCommand)} - Command: {command.CommandType}");
            }

            return await command.Awaiter;
        }

        /// <summary>
        /// Executes the specified delayed command.
        /// </summary>
        /// <param name="argument">The argument.</param>
        /// <param name="seekMode">The seek mode.</param>
        /// <returns>
        /// The awaitable task handle
        /// </returns>
        private async Task<bool> ExecuteDelayedSeekCommand(TimeSpan argument, SeekCommand.SeekMode seekMode)
        {
            if (CanExecuteQueuedCommands == false)
            {
                DecrementPendingSeeks();
                return false;
            }

            SeekCommand currentCommand;
            lock (QueueLock)
            {
                currentCommand = CommandQueue
                    .FirstOrDefault(c => c.CommandType == CommandType.Seek) as SeekCommand;

                if (currentCommand != null)
                    currentCommand.TargetPosition = argument;
            }

            if (currentCommand != null)
            {
                DecrementPendingSeeks();
                return await currentCommand.Awaiter;
            }

            var command = new SeekCommand(MediaCore, argument, seekMode);
            lock (QueueLock)
                CommandQueue.Add(command);

            return await command.Awaiter;
        }

        /// <summary>
        /// Tries the enter direct command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>If direct command entering was successful</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryEnterDirectCommand(DirectCommandBase command)
        {
            lock (DirectLock)
            {
                // Prevent running a new priority event if one is already in progress
                if (IsDisposed || DirectCommandEvent.IsInProgress)
                {
                    command.Dispose();
                    return false;
                }

                // Signal the workers they need to wait
                DirectCommandEvent.Begin();
                CurrentDirectCommand = command;

                // Update the state
                lock (StatusLock)
                {
                    m_IsOpening = command.CommandType == CommandType.Open;
                    m_IsClosing = command.CommandType == CommandType.Close;
                    m_IsChanging = command.CommandType == CommandType.ChangeMedia;
                }
            }

            return true;
        }

        /// <summary>
        /// Prepares for direct command.
        /// </summary>
        /// <param name="commandType">Type of the command.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PrepareForDirectCommand(CommandType commandType)
        {
            // Always pause the clock when opening, closing or changing media
            MediaCore.Clock.Pause();

            // Always signal a manual change of state.
            MediaCore.State.UpdateMediaState(PlaybackStatus.Manual);

            // Clear any commands that have been queued. Direct commands
            // take over all pending commands.
            ClearCommandQueue();

            // Signal the workers to stop
            if (commandType == CommandType.Close)
            {
                // Prepare for close command by signalling workers to stop
                IsStopWorkersPending = true;

                // Signal the container reads to abort immediately
                MediaCore.Container?.SignalAbortReads(false);
            }

            // Wait for cycles to complete.
            // Cycles must wait for priority commands before continuing
            if (!MediaCore.State.IsOpen) return;

            MediaCore.FrameDecodingCycle.Wait();
            MediaCore.Workers.Pause();
        }

        /// <summary>
        /// Finalizes the direct command by performing command post-processing.
        /// </summary>
        /// <param name="command">The command.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FinalizeDirectCommand(DirectCommandBase command)
        {
            lock (StatusLock)
            {
                m_IsOpening = false;
                m_IsClosing = false;
                m_IsChanging = false;
            }

            lock (DirectLock)
            {
                CurrentDirectCommand = null;
                DirectCommandEvent.Complete();
                MediaCore.Workers.Resume();
            }

            command.PostProcess();
        }

        #endregion
    }
}
