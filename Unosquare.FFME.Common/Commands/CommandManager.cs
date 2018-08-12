namespace Unosquare.FFME.Commands
{
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides centralized access to playback controls
    /// </summary>
    /// <seealso cref="IDisposable" />
    internal sealed class CommandManager : IDisposable
    {
        #region Private Members

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

        private bool m_IsClosing = default;
        private bool m_IsOpening = default;
        private bool m_IsChanging = default;
        private bool m_IsDisposed = default;

        private DirectCommandBase CurrentDirectCommand;
        private CommandBase CurrentQueueCommand;

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

        /// <summary>
        /// Gets the associated media core.
        /// </summary>
        public MediaEngine MediaCore { get; }

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
        public bool IsSeeking
        {
            get => IsActivelySeeking || HasQueuedSeekCommands;
        }

        /// <summary>
        /// Gets a value indicating whether a seek command is currently executing.
        /// This differs from the <see cref="IsSeeking"/> property as this is the realtime
        /// state of a seek operation as opposed to a general, delayed state of the command manager.
        /// </summary>
        public bool IsActivelySeeking { get => SeekingCommandEvent.IsInProgress; }

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
        /// Gets a value indicating whether the command queued constains commands.
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
                    if (m_IsOpening == false && MediaCore.State.IsOpen == false) return false;

                    return true;
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

            return await ExecuteDirectCommand(new DirectOpenCommand(MediaCore, uri));
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

            return await ExecuteDirectCommand(new DirectOpenCommand(MediaCore, stream));
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
            else if (IsExecutingDirectCommand)
                return false;
            else
                return await ExecuteDirectCommand(new DirectCloseCommand(MediaCore));
        }

        /// <summary>
        /// Begins the process of changing/updating media paramaeters.
        /// </summary>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> ChangeMediaAsync()
        {
            if (MediaCore.State.IsOpen == false || IsDisposed)
                return false;

            var currentCommand = GetCurrentDirectCommand(CommandType.ChangeMedia);

            if (currentCommand != null)
                return await currentCommand.Awaiter;
            else if (IsExecutingDirectCommand)
                return false;
            else
                return await ExecuteDirectCommand(new DirectChangeCommand(MediaCore));
        }

        /// <summary>
        /// Begins playback of the media.
        /// </summary>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> PlayAsync() =>
            await ExecuteProrityCommand(CommandType.Play);

        /// <summary>
        /// Pauses the playback of the media.
        /// </summary>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> PauseAsync() =>
            await ExecuteProrityCommand(CommandType.Pause);

        /// <summary>
        /// Stops the playback of the media.
        /// </summary>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> StopAsync()
        {
            IncrementPendingSeeks();
            var stopTask = ExecuteProrityCommand(CommandType.Stop);
            return await stopTask;
        }

        /// <summary>
        /// Seeks to the target position on the media
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> SeekAsync(TimeSpan target)
        {
            IncrementPendingSeeks();
            var seekTask = ExecuteDelayedSeekCommand(target);
            return await seekTask;
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
                    CurrentQueueCommand = command;
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
                        PlayAfterSeek.Value = MediaCore.Clock.IsRunning &&
                            command.CommandType != CommandType.Stop;
                        MediaCore.SendOnSeekingStarted();
                    }
                }

                // Execute the command synchronously
                command.Execute();
            }
            catch { throw; }
            finally
            {
                lock (QueueLock)
                {
                    if (command.AffectsSeekingState)
                    {
                        SeekingCommandEvent.Complete();
                        DecrementPendingSeeks(wasCancelled: false);
                    }

                    CurrentQueueCommand = null;
                }
            }

            return command.CommandType;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() =>
            Dispose(true);

        #endregion

        #region Private Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecrementPendingSeeks(bool wasCancelled)
        {
            lock (QueueLock)
            {
                PendingSeekCount.Value--;
                if (PendingSeekCount <= 0)
                {
                    // Reset the pending Seek Count
                    PendingSeekCount.Value = 0;

                    // Notify the end of a seek if we previously notified the
                    // the start of one.
                    if (HasSeekingStarted == true)
                    {
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
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementPendingSeeks() { lock (QueueLock) PendingSeekCount.Value++; }

        /// <summary>
        /// Clears the command queue.
        /// All commands are signalled so all awaiters stop awaiting.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearCommandQueue()
        {
            lock (QueueLock)
            {
                CommandBase command = null;

                for (var i = CommandQueue.Count - 1; i >= 0; i--)
                {
                    command = CommandQueue[i];
                    command.Dispose();

                    if (command.AffectsSeekingState)
                        DecrementPendingSeeks(wasCancelled: true);
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
                    CurrentDirectCommand.CommandType == CommandType.Close)
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
            catch { throw; }
            finally { FinalizeDirectCommand(command); }
        }

        /// <summary>
        /// Executes the specified prority command.
        /// </summary>
        /// <param name="commandType">Type of the command.</param>
        /// <returns>The awaitable task handle</returns>
        private async Task<bool> ExecuteProrityCommand(CommandType commandType)
        {
            if (CanExecuteQueuedCommands == false)
            {
                if (CommandBase.TypeAffectsSeekingState(commandType))
                    DecrementPendingSeeks(wasCancelled: true);

                return false;
            }

            CommandBase currentCommand = null;
            lock (QueueLock)
            {
                if (CurrentQueueCommand != null &&
                    CurrentQueueCommand.CommandType == commandType)
                {
                    currentCommand = CurrentQueueCommand;
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
                DecrementPendingSeeks(wasCancelled: true);
                return await currentCommand.Awaiter;
            }

            CommandBase command = null;

            switch (commandType)
            {
                case CommandType.Play:
                    command = new PlayCommand(MediaCore);
                    break;
                case CommandType.Pause:
                    command = new PauseCommand(MediaCore);
                    break;
                case CommandType.Stop:
                    command = new StopCommand(MediaCore);
                    break;
                default:
                    throw new ArgumentException($"{nameof(commandType)} is of invalid type '{commandType}'");
            }

            // Priority commands clear the queue and add themselves.
            lock (QueueLock)
            {
                ClearCommandQueue();
                CommandQueue.Add(command);
            }

            return await command.Awaiter;
        }

        /// <summary>
        /// Executes the specified delayed command.
        /// </summary>
        /// <param name="argument">The argument.</param>
        /// <returns>The awaitable task handle</returns>
        private async Task<bool> ExecuteDelayedSeekCommand(TimeSpan argument)
        {
            if (CanExecuteQueuedCommands == false)
            {
                DecrementPendingSeeks(wasCancelled: true);
                return false;
            }

            SeekCommand currentCommand = null;
            lock (QueueLock)
            {
                currentCommand = CommandQueue
                    .FirstOrDefault(c => c.CommandType == CommandType.Seek) as SeekCommand;

                if (currentCommand != null)
                    currentCommand.TargetPosition = argument;
            }

            if (currentCommand != null)
            {
                DecrementPendingSeeks(wasCancelled: true);
                return await currentCommand.Awaiter;
            }

            var command = new SeekCommand(MediaCore, argument);
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
            // Always pause the clock when opening, closing or changin media
            MediaCore?.Clock.Pause();

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
            if (MediaCore.State.IsOpen)
            {
                MediaCore.FrameDecodingCycle.Wait();
                MediaCore.PacketReadingCycle.Wait();
            }
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
            }

            command.PostProcess();
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged">
        ///   <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            lock (DisposeLock)
            {
                if (m_IsDisposed) return;

                // Immediately mark as disposed so no more
                // Commands can be executed/enqueued
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
    }
}
