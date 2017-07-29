namespace Unosquare.FFME.Commands
{
    using Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Threading;

    internal sealed class MediaCommandManager
    {
        #region Private Declarations

        private readonly object SyncLock = new object();
        private readonly List<MediaCommand> Commands = new List<MediaCommand>();
        private readonly MediaElement m_MediaElement;
        private MediaCommand m_ExecutingCommand = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaCommandManager"/> class.
        /// </summary>
        /// <param name="mediaElement">The media element.</param>
        public MediaCommandManager(MediaElement mediaElement)
        {
            m_MediaElement = mediaElement;
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
        /// Gets or sets the currently executing command.
        /// If there are no commands being executed, then it returns null;
        /// </summary>
        public MediaCommand ExecutingCommand
        {
            get { lock (SyncLock) { return m_ExecutingCommand; } }
            set { lock (SyncLock) { m_ExecutingCommand = value; } }
        }

        /// <summary>
        /// Gets the parent media element.
        /// </summary>
        public MediaElement MediaElement
        {
            get { return m_MediaElement; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Opens the specified URI.
        /// The command is processed in a Thread Pool Thread.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The awaitable task</returns>
        public async Task Open(Uri uri)
        {
            lock (SyncLock)
            {
                Commands.Clear();
            }

            // Process the command in a background thread as opposed
            // to in the thread that it was called to prevent blocking.
            await Task.Run(async () =>
            {
                var command = new OpenCommand(this, uri);
                await command.ExecuteAsync();
            });
        }

        /// <summary>
        /// Starts playing the open media URI.
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task Play()
        {
            PlayCommand command = null;

            lock (SyncLock)
            {
                command = Commands.FirstOrDefault(c => c.CommandType == MediaCommandType.Play) as PlayCommand;
                if (command == null)
                {
                    command = new PlayCommand(this);
                    Commands.Add(command);
                }
            }

            await command.Promise;
        }

        /// <summary>
        /// Pauses the media.
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task Pause()
        {
            PauseCommand command = null;

            lock (SyncLock)
            {
                command = Commands.FirstOrDefault(c => c.CommandType == MediaCommandType.Pause) as PauseCommand;
                if (command == null)
                {
                    command = new PauseCommand(this);
                    Commands.Add(command);
                }
            }

            await command.Promise;
        }

        /// <summary>
        /// Pauses and rewinds the media
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task Stop()
        {
            StopCommand command = null;

            lock (SyncLock)
            {
                command = Commands.FirstOrDefault(c => c.CommandType == MediaCommandType.Stop) as StopCommand;
                if (command == null)
                {
                    command = new StopCommand(this);
                    Commands.Add(command);
                }
            }

            await command.Promise;
        }

        /// <summary>
        /// Seeks to the specified position within the media.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The awaitable task</returns>
        public async Task Seek(TimeSpan position)
        {
            SeekCommand command = null;
            lock (SyncLock)
            {
                command = Commands.FirstOrDefault(c => c.CommandType == MediaCommandType.Seek) as SeekCommand;
                if (command == null)
                {
                    command = new SeekCommand(this, position);
                    Commands.Add(command);
                }
                else
                {
                    command.TargetPosition = position;
                }
            }

            await command.Promise;
        }

        /// <summary>
        /// Closes the specified media.
        /// This command gets processed in a threadpool thread.
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task Close()
        {
            lock (SyncLock)
            {
                Commands.Clear();
            }

            // Process the command in a background thread as opposed
            // to in the thread that it was called to prevent blocking.
            await Dispatcher.CurrentDispatcher.PumpInvokeAsync((Action)(() => 
            {
                var command = new CloseCommand(this);
                command.Execute();
            }));
        }

        /// <summary>
        /// Sets the playback speed ratio.
        /// </summary>
        /// <param name="targetSpeedRatio">The target speed ratio.</param>
        /// <returns>The awaitable task</returns>
        public async Task SetSpeedRatio(double targetSpeedRatio)
        {
            SpeedRatioCommand command = null;
            lock (SyncLock)
            {
                command = Commands.FirstOrDefault(c => c.CommandType == MediaCommandType.SetSpeedRatio) as SpeedRatioCommand;
                if (command == null)
                {
                    command = new SpeedRatioCommand(this, targetSpeedRatio);
                    Commands.Add(command);
                }
                else
                {
                    command.SpeedRatio = targetSpeedRatio;
                }
            }

            await command.Promise;
        }

        /// <summary>
        /// Processes the next command in the command queue.
        /// This method is called in every block rendering cycle.
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task ProcessNext()
        {
            MediaCommand command = null;

            lock (SyncLock)
            {
                if (Commands.Count == 0) return;
                command = Commands[0];
                Commands.RemoveAt(0);
            }

            await command.ExecuteAsync();
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

        #endregion

    }
}
