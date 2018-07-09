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
        private readonly IWaitEvent DirectCommandEvent = null;
        private readonly IWaitEvent SeekingDone = null;
        private readonly AtomicBoolean m_IsStopWorkersPending = new AtomicBoolean(false);
        private readonly AtomicBoolean m_IsSeeking = new AtomicBoolean(false);

        private readonly object DirectLock = new object();
        private readonly object QueueLock = new object();
        private readonly object StatusLock = new object();
        private readonly object DisposeLock = new object();

        private bool m_IsClosing = default;
        private bool m_IsOpening = default;
        private bool m_IsChanging = default;
        private bool m_IsDisposed = default;
        private bool PlayAfterSeek = default;

        private DirectCommandBase CurrentDirectCommand = null;
        private CommandBase CurrentQueueCommand = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandManager" /> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public CommandManager(MediaEngine mediaCore)
        {
            DirectCommandEvent = WaitEventFactory.Create(isCompleted: true, useSlim: true);
            SeekingDone = WaitEventFactory.Create(isCompleted: true, useSlim: true);
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
            get => m_IsSeeking.Value == true;
            private set => m_IsSeeking.Value = value;
        }

        /// <summary>
        /// Gets a value indicating whether a seek command is currently executing.
        /// This differs from the <see cref="IsSeeking"/> property as this is the realtime
        /// state of a seek operation as opposed to a general, delayed state of the command manager.
        /// </summary>
        public bool IsActivelySeeking { get => SeekingDone.IsInProgress; }

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
        /// Gets a value indicating whether the command queue contains seek commands.
        /// </summary>
        public bool HasQueuedSeekCommands
        {
            get { lock (QueueLock) return CommandQueue.Any(c => c.CommandType == CommandType.Seek); }
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
        public async Task<bool> StopAsync() =>
            await ExecuteProrityCommand(CommandType.Stop);

        /// <summary>
        /// Seeks to the target position on the media
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> SeekAsync(TimeSpan target) =>
                    await ExecuteDelayedCommand(CommandType.Seek, target);

        /// <summary>
        /// Sets the speed ratio on the media.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>The awaitable task. The task result determines if the command was successfully started</returns>
        public async Task<bool> SetSpeedRatioAsync(double target) =>
                    await ExecuteDelayedCommand(CommandType.SpeedRatio, target);

        /// <summary>
        /// Waits for any current direct command to finish execution.
        /// </summary>
        public void WaitForDirectCommand() =>
            DirectCommandEvent.Wait();

        /// <summary>
        /// Waits for an active seek command (if any) to complete.
        /// </summary>
        public void WaitForActiveSeekCommand() =>
            SeekingDone.Wait();

        /// <summary>
        /// Executes the next command in the queued.
        /// </summary>
        public void ExecuteNextQueuedCommand()
        {
            if (DirectCommandEvent.IsInProgress)
                return;

            CommandBase command = null;
            lock (QueueLock)
            {
                DetectSeekingStarted();

                if (CommandQueue.Count > 0)
                {
                    command = CommandQueue[0];
                    CommandQueue.RemoveAt(0);
                    CurrentQueueCommand = command;
                }
            }

            try
            {
                if (command != null)
                {
                    // Initiate a seek cycle
                    if (command.CommandType == CommandType.Seek)
                        SeekingDone.Begin();

                    // Execute the command synchronously
                    command?.Execute();
                }
            }
            catch { throw; }
            finally
            {
                SeekingDone.Complete();
                lock (QueueLock)
                {
                    DetectSeekingEnded();
                    CurrentQueueCommand = null;
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() =>
            Dispose(true);

        #endregion

        #region Private Methods

        /// <summary>
        /// Detects the seeking started operation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DetectSeekingStarted()
        {
            var m = MediaCore;

            // Check if we have pending seeks to notify the start of a seek operation
            if (m.State.IsSeeking == false && HasQueuedSeekCommands)
            {
                PlayAfterSeek = m.Clock.IsRunning;
                IsSeeking = true;
                m.SendOnSeekingStarted();
            }
        }

        /// <summary>
        /// Detects the seeking ended operation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DetectSeekingEnded()
        {
            var m = MediaCore;

            // Don't do a thing if we are not currently seeking or if we still
            // have seek commands in the queue.
            if (IsSeeking == false || HasQueuedCommands) return;

            // Detect a end of seek cycle and update the state to the final position
            // as set by the seek command. This is the position we already captured
            // as the seek command was processed already.
            m.State.UpdatePosition(m.WallClock);

            // Call the seek method on all renderers
            foreach (var kvp in m.Renderers)
                m.InvalidateRenderer(kvp.Key);

            m.SendOnSeekingEnded();
            ResumePlayAfterSeek();
        }

        /// <summary>
        /// Resumes playback if the clock was running prior to the start of a seek operation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResumePlayAfterSeek()
        {
            lock (QueueLock)
            {
                if (IsSeeking == false) return;

                if (PlayAfterSeek)
                {
                    MediaCore.Clock.Play();
                    MediaCore.State.UpdateMediaState(PlaybackStatus.Play, MediaCore.WallClock);
                }
                else
                {
                    MediaCore.State.UpdateMediaState(PlaybackStatus.Pause, MediaCore.WallClock);
                }

                PlayAfterSeek = false;
                IsSeeking = false;
            }
        }

        /// <summary>
        /// Clears the command queue.
        /// All commands are signalled so all awaiters stop awaiting.
        /// </summary>
        private void ClearQueuedCommands()
        {
            lock (QueueLock)
            {
                for (var i = CommandQueue.Count - 1; i >= 0; i--)
                    CommandQueue[i].Dispose();

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
            if (CanExecuteQueuedCommands == false) return false;

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
                return await currentCommand.Awaiter;

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

            lock (QueueLock)
            {
                ClearQueuedCommands();
                CommandQueue.Add(command);
            }

            return await command.Awaiter;
        }

        /// <summary>
        /// Executes the specified delayed command.
        /// </summary>
        /// <typeparam name="T">The agument type for the command</typeparam>
        /// <param name="commandType">Type of the command.</param>
        /// <param name="argument">The argument.</param>
        /// <returns>The awaitable task handle</returns>
        private async Task<bool> ExecuteDelayedCommand<T>(CommandType commandType, T argument)
        {
            if (CanExecuteQueuedCommands == false) return false;

            var timeSpanArgument = commandType == CommandType.Seek ?
                (TimeSpan)Convert.ChangeType(argument, typeof(TimeSpan)) :
                TimeSpan.Zero;

            var doubleArgument = commandType == CommandType.SpeedRatio ?
                (double)Convert.ChangeType(argument, typeof(double)) : default;

            CommandBase currentCommand = null;
            lock (QueueLock)
            {
                currentCommand = CommandQueue
                    .FirstOrDefault(c => c.CommandType == commandType);

                if (currentCommand != null)
                {
                    switch (commandType)
                    {
                        case CommandType.Seek:
                            (currentCommand as SeekCommand).TargetPosition = timeSpanArgument;
                            break;
                        case CommandType.SpeedRatio:
                            (currentCommand as SpeedRatioCommand).SpeedRatio = doubleArgument;
                            break;
                        default:
                            throw new ArgumentException($"{nameof(commandType)} is of invalid type '{commandType}'");
                    }
                }
            }

            if (currentCommand != null)
                return await currentCommand.Awaiter;

            CommandBase command = null;

            switch (commandType)
            {
                case CommandType.Seek:
                    command = new SeekCommand(MediaCore, timeSpanArgument);
                    break;
                case CommandType.SpeedRatio:
                    command = new SpeedRatioCommand(MediaCore, doubleArgument);
                    break;
                default:
                    throw new ArgumentException($"{nameof(commandType)} is of invalid type '{commandType}'");
            }

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
            ClearQueuedCommands();

            // Signal the workers to stop
            if (commandType == CommandType.Close)
            {
                IsSeeking = false;
                PlayAfterSeek = false;
                SeekingDone.Complete();

                // Prepare for close command by signalling workers to stop
                IsStopWorkersPending = true;

                // Signal the reads to abort
                MediaCore.Container?.SignalAbortReads(false);
            }
            else if (commandType == CommandType.Open)
            {
                IsSeeking = false;
                PlayAfterSeek = false;
                SeekingDone.Complete();
            }
            else if (commandType == CommandType.ChangeMedia)
            {
                // Signal the start of a changing event
                // and check if we play after mediachanged
                SeekingDone.Begin();
                IsSeeking = true;
                PlayAfterSeek = MediaCore.Clock.IsRunning;
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

            try
            {
                command.PostProcess();
            }
            catch
            {
                PlayAfterSeek = false;
                throw;
            }
            finally
            {
                SeekingDone.Complete();
                ResumePlayAfterSeek();
            }
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
                ClearQueuedCommands();
                IsStopWorkersPending = true;
                MediaCore.Container?.SignalAbortReads(false);

                // Wait for any pending direct command
                DirectCommandEvent.Wait();

                // Run the close command directly
                var closeCommand = new DirectCloseCommand(MediaCore);
                closeCommand.Execute();

                // Dispose of additional resources.
                DirectCommandEvent?.Dispose();
                SeekingDone?.Dispose();
            }
        }

        #endregion
    }
}
