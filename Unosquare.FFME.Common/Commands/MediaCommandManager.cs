namespace Unosquare.FFME.Commands
{
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a single point of contact for media command excution.
    /// </summary>
    internal sealed class MediaCommandManager
    {
        #region Private Declarations

        private readonly AtomicBoolean IsOpening = new AtomicBoolean(false);
        private readonly AtomicBoolean IsClosing = new AtomicBoolean(false);
        private readonly object SyncLock = new object();
        private readonly List<MediaCommand> Commands = new List<MediaCommand>();
        private readonly MediaEngine m_MediaCore;
        private MediaCommand ExecutingCommand = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaCommandManager"/> class.
        /// </summary>
        /// <param name="mediaEngine">The media element.</param>
        public MediaCommandManager(MediaEngine mediaEngine)
        {
            m_MediaCore = mediaEngine;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of commands pending execution.
        /// </summary>
        public int PendingCount
        {
            get { lock (SyncLock) return Commands.Count; }
        }

        /// <summary>
        /// Gets the core platform independent player component.
        /// </summary>
        public MediaEngine MediaCore => m_MediaCore;

        #endregion

        #region Methods

        /// <summary>
        /// Gets a value indicating whether commands can be executed.
        /// Returns false if an Opening or Closing Command is in progress.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can execute commands; otherwise, <c>false</c>.
        /// </value>
        private bool CanExecuteCommands
        {
            get
            {
                if (MediaCore == null || MediaCore.IsDisposed)
                {
                    MediaCore?.Log(
                        MediaLogMessageType.Warning,
                        $"{nameof(MediaCommandManager)}: Associated {nameof(MediaCore)} is null, closing, or disposed.");

                    return false;
                }

                if (IsOpening.Value || IsClosing.Value || MediaCore.State.IsOpening)
                {
                    MediaCore?.Log(
                        MediaLogMessageType.Warning,
                        $"{nameof(MediaCommandManager)}: Operation already in progress."
                        + $" {nameof(IsOpening)} = {IsOpening.Value}; {nameof(IsClosing)} = {IsClosing.Value}.");

                    return false;
                }

                return true;
            }
        }

        #region Synchronous, Direct Command Handlers: Priority 0

        /// <summary>
        /// Opens the specified URI.
        /// This command gets processed in a threadpool thread asynchronously.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The asynchronous task</returns>
        public async Task OpenAsync(Uri uri)
        {
            // Check Uri Argument
            if (uri == null)
            {
                MediaCore?.Log(
                    MediaLogMessageType.Warning,
                    $"{nameof(MediaCommandManager)}.{nameof(OpenAsync)}: '{nameof(uri)}' cannot be null");

                return;
            }

            if (CanExecuteCommands == false)
                return;
            else
                IsOpening.Value = true;

            var command = new OpenCommand(this, uri);
            ExecutingCommand = command;
            ClearCommandQueue();

            var action = new Action(() =>
            {
                try
                {
                    if (command.HasCompleted) return;
                    command.RunSynchronously();
                }
                catch (Exception ex)
                {
                    MediaCore?.Log(
                        MediaLogMessageType.Error,
                        $"{nameof(MediaCommandManager)}.{nameof(OpenAsync)}: {ex.GetType()} - {ex.Message}");
                }
                finally
                {
                    ExecutingCommand?.Complete();
                    ExecutingCommand = null;
                    IsOpening.Value = false;
                }
            });

            await Task.Run(action);
        }

        /// <summary>
        /// Closes the specified media.
        /// This command gets processed in a threadpool thread asynchronously.
        /// </summary>
        /// <returns>Returns the background task.</returns>
        public async Task CloseAsync()
        {
            if (CanExecuteCommands == false)
                return;
            else
                IsClosing.Value = true;

            var command = new CloseCommand(this);
            ExecutingCommand = command;
            ClearCommandQueue();

            var action = new Action(() =>
            {
                try
                {
                    if (command.HasCompleted) return;
                    command.RunSynchronously();
                }
                catch (Exception ex)
                {
                    MediaCore?.Log(
                        MediaLogMessageType.Error,
                        $"{nameof(MediaCommandManager)}.{nameof(CloseAsync)}: {ex.GetType()} - {ex.Message}");
                }
                finally
                {
                    ExecutingCommand?.Complete();
                    ExecutingCommand = null;
                    IsClosing.Value = false;
                }
            });

            await Task.Run(action);
        }

        #endregion

        #region Singleton, Asynchronous Command Handlers: Priority 1

        /// <summary>
        /// Starts playing the open media URI.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task PlayAsync()
        {
            PlayCommand command = null;

            lock (SyncLock)
            {
                command = Commands.FirstOrDefault(c => c.CommandType == MediaCommandType.Play) as PlayCommand;
                if (command == null)
                {
                    command = new PlayCommand(this);
                    EnqueueCommand(command);
                }
            }

            await command.TaskContext;
        }

        /// <summary>
        /// Pauses the media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task PauseAsync()
        {
            PauseCommand command = null;

            lock (SyncLock)
            {
                command = Commands.FirstOrDefault(c => c.CommandType == MediaCommandType.Pause) as PauseCommand;
                if (command == null)
                {
                    command = new PauseCommand(this);
                    EnqueueCommand(command);
                }
            }

            await command.TaskContext;
        }

        /// <summary>
        /// Pauses and rewinds the media
        /// This command invalidates all queued commands
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task StopAsync()
        {
            StopCommand command = null;

            lock (SyncLock)
            {
                command = Commands.FirstOrDefault(c => c.CommandType == MediaCommandType.Stop) as StopCommand;
                if (command == null)
                {
                    command = new StopCommand(this);
                    EnqueueCommand(command);
                }
            }

            await command.TaskContext;
        }

        #endregion

        #region Queued, Asynchronous Command Handlers: Priority 2

        /// <summary>
        /// Seeks to the specified position within the media.
        /// This command is a queued command
        /// </summary>
        /// <param name="position">The position.</param>
        public void EnqueueSeek(TimeSpan position)
        {
            SeekCommand command = null;
            lock (SyncLock)
            {
                command = Commands.LastOrDefault(c => c.CommandType == MediaCommandType.Seek) as SeekCommand;
                if (command == null || command.IsRunning)
                {
                    command = new SeekCommand(this, position);
                    EnqueueCommand(command);
                }
                else
                {
                    command.TargetPosition = position;
                }
            }
        }

        /// <summary>
        /// Sets the playback speed ratio.
        /// This command is a queued command
        /// </summary>
        /// <param name="targetSpeedRatio">The target speed ratio.</param>
        public void EnqueueSpeedRatio(double targetSpeedRatio)
        {
            SpeedRatioCommand command = null;
            lock (SyncLock)
            {
                command = Commands.LastOrDefault(c => c.CommandType == MediaCommandType.SetSpeedRatio) as SpeedRatioCommand;
                if (command == null)
                {
                    command = new SpeedRatioCommand(this, targetSpeedRatio);
                    EnqueueCommand(command);
                }
                else
                {
                    command.SpeedRatio = targetSpeedRatio;
                }
            }
        }

        #endregion

        /// <summary>
        /// Processes the next command in the command queue.
        /// This method is called in every block rendering cycle.
        /// </summary>
        public void ProcessNext()
        {
            DumpQueue($"Before {nameof(ProcessNext)}", false);
            if (MediaCore.IsTaskCancellationPending)
                return;

            MediaCommand command = null;

            lock (SyncLock)
            {
                if (Commands.Count == 0) return;
                command = Commands[0];
                Commands.RemoveAt(0);
            }

            try
            {
                ExecutingCommand = command;
                command.RunSynchronously();
                DumpQueue($"After {nameof(ProcessNext)}", false);
            }
            catch (Exception ex)
            {
                MediaCore?.Log(MediaLogMessageType.Error, $"{ex.GetType()}: {ex.Message}");
                throw;
            }
            finally
            {
                ExecutingCommand = null;
            }
        }

        /// <summary>
        /// Gets the pending count of the given command type.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>The amount of commands of the given type</returns>
        public int PendingCountOf(MediaCommandType t)
        {
            lock (SyncLock)
            {
                return Commands.Count(c => c.CommandType == t);
            }
        }

        /// <summary>
        /// Enqueues the command for execution.
        /// </summary>
        /// <param name="command">The command.</param>
        private void EnqueueCommand(MediaCommand command)
        {
            if (MediaCore.State.IsOpen == false)
            {
                command.Complete();
                return;
            }

            lock (SyncLock)
                Commands.Add(command);
        }

        /// <summary>
        /// Outputs the state of the queue
        /// </summary>
        /// <param name="operation">The operation.</param>
        /// <param name="outputEmpty">if set to <c>true</c> [output empty].</param>
        private void DumpQueue(string operation, bool outputEmpty)
        {
            if (MediaEngine.Platform.IsInDebugMode == false)
                return;

            lock (SyncLock)
            {
                if (outputEmpty == false && Commands.Count <= 0) return; // Prevent output for empty commands
                MediaCore.Log(MediaLogMessageType.Trace, $"Command Queue ({Commands.Count} commands): {operation}");
                foreach (var c in Commands)
                {
                    MediaCore.Log(MediaLogMessageType.Trace, $"   {c.ToString()}");
                }
            }
        }

        /// <summary>
        /// Clears the command queue.
        /// </summary>
        private void ClearCommandQueue()
        {
            lock (SyncLock)
            {
                // Mark every command as completed
                foreach (var command in Commands)
                    command?.Complete();

                // Clear all commands from Queue
                Commands.Clear();
            }
        }

        #endregion

    }
}
