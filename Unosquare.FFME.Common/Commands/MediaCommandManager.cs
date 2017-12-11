namespace Unosquare.FFME.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Core;

    /// <summary>
    /// Represents a singlo point of contact for media command excution.
    /// </summary>
    internal sealed class MediaCommandManager
    {
        #region Private Declarations

        private readonly AtomicBoolean IsOpening = new AtomicBoolean() { Value = false };
        private readonly AtomicBoolean IsClosing = new AtomicBoolean() { Value = false };
        private readonly object SyncLock = new object();
        private readonly List<MediaCommand> Commands = new List<MediaCommand>();
        private readonly MediaElementCore m_MediaElement;

        private MediaCommand ExecutingCommand = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaCommandManager"/> class.
        /// </summary>
        /// <param name="mediaElementCore">The media element.</param>
        public MediaCommandManager(MediaElementCore mediaElementCore)
        {
            m_MediaElement = mediaElementCore;
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
        public MediaElementCore MediaElement => m_MediaElement;

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
                if (MediaElement == null || MediaElement.IsDisposed)
                {
                    MediaElement?.Logger.Log(
                        MediaLogMessageType.Warning,
                        $"{nameof(MediaCommandManager)}: Associated {nameof(MediaElement)} is null, closing, or disposed.");

                    return false;
                }

                if (IsOpening.Value || IsOpening.Value || MediaElement.IsOpening)
                {
                    MediaElement?.Logger.Log(
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
        /// The command is processed in a Thread Pool Thread.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The awaitable Open operation</returns>
        public Task Open(Uri uri)
        {
            // Check Uri Argument
            if (uri == null)
            {
                MediaElement?.Logger.Log(
                    MediaLogMessageType.Warning,
                    $"{nameof(MediaCommandManager)}.{nameof(Open)}: '{nameof(uri)}' cannot be null");

                return Platform.CreatePumpOperation();
            }

            if (CanExecuteCommands == false)
                return Platform.CreatePumpOperation();
            else
                IsOpening.Value = true;

            var command = new OpenCommand(this, uri);
            ExecutingCommand = command;
            ClearCommandQueue();

            var backgroundTask = Task.Run(() => 
            {
                try
                {
                    if (command.HasCompleted) return;
                    command.Execute();
                }
                catch (Exception ex)
                {
                    MediaElement?.Logger.Log(
                        MediaLogMessageType.Error,
                        $"{nameof(MediaCommandManager)}.{nameof(Open)}: {ex.GetType()} - {ex.Message}");
                }
                finally
                {
                    ExecutingCommand?.Complete();
                    ExecutingCommand = null;
                    IsOpening.Value = false;
                }

                System.Diagnostics.Debug.Assert(
                    MediaElement.IsOpen == true && MediaElement.IsOpening == false && command.HasCompleted,
                    "Synchronous conditions not met");
            });

            var operation = Platform.CreateAsynchronousPumpWaiter(backgroundTask);
            return operation;
        }

        /// <summary>
        /// Closes the specified media.
        /// This command gets processed in a threadpool thread.
        /// </summary>
        /// <returns>The awaitable close operation</returns>
        public Task Close()
        {
            if (CanExecuteCommands == false)
                return Platform.CreatePumpOperation();
            else
                IsClosing.Value = true;

            var command = new CloseCommand(this);
            ExecutingCommand = command;
            ClearCommandQueue();

            var backgroundTask = Task.Run(() =>
            {
                try
                {
                    if (command.HasCompleted) return;
                    command.Execute();
                }
                catch (Exception ex)
                {
                    MediaElement?.Logger.Log(
                        MediaLogMessageType.Error,
                        $"{nameof(MediaCommandManager)}.{nameof(Close)}: {ex.GetType()} - {ex.Message}");
                }
                finally
                {
                    ExecutingCommand?.Complete();
                    ExecutingCommand = null;
                    IsClosing.Value = false;
                }
            });

            var operation = Platform.CreateAsynchronousPumpWaiter(backgroundTask);
            return operation;
        }

        #endregion

        #region Singleton, Asynchronous Command Handlers: Priority 1

        /// <summary>
        /// Starts playing the open media URI.
        /// </summary>
        public void Play()
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

            WaitFor(command);
        }

        /// <summary>
        /// Pauses the media.
        /// </summary>
        public void Pause()
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

            WaitFor(command);
        }

        /// <summary>
        /// Pauses and rewinds the media
        /// This command invalidates all queued commands
        /// </summary>
        public void Stop()
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

            WaitFor(command);
        }

        #endregion

        #region Queued, Asynchronous Command Handlers: Priority 2

        /// <summary>
        /// Seeks to the specified position within the media.
        /// This command is a queued command
        /// </summary>
        /// <param name="position">The position.</param>
        public void Seek(TimeSpan position)
        {
            SeekCommand command = null;
            lock (SyncLock)
            {
                command = Commands.LastOrDefault(c => c.CommandType == MediaCommandType.Seek) as SeekCommand;
                if (command == null)
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
        public void SetSpeedRatio(double targetSpeedRatio)
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
            MediaCommand command = null;

            lock (SyncLock)
            {
                if (Commands.Count == 0) return;
                command = Commands[0];
                Commands.RemoveAt(0);
            }

            command.Execute();
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
            if (MediaElement.IsOpen == false)
            {
                command.Complete();
                return;
            }

            lock (SyncLock)
                Commands.Add(command);
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

        /// <summary>
        /// Waits for the command to complete execution.
        /// </summary>
        /// <param name="command">The command.</param>
        private void WaitFor(MediaCommand command)
        {
            var waitTask = Task.Run(async () =>
            {
                while (command.HasCompleted == false && MediaElement.IsOpen)
                    await Task.Delay(10);
            });

            while (waitTask.IsCompleted == false)
            {
                // Pump invoke
                Platform.UIInvoke(
                    CoreDispatcherPriority.Background,
                    () => { });
            }
        }

        #endregion

    }
}
