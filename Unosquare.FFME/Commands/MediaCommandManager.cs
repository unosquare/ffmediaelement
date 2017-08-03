namespace Unosquare.FFME.Commands
{
    using Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Threading;

    /// <summary>
    /// Represents a singlo point of contact for media command excution.
    /// </summary>
    internal sealed class MediaCommandManager
    {
        #region Private Declarations

        private readonly object SyncLock = new object();
        private readonly List<MediaCommand> Commands = new List<MediaCommand>();
        private readonly MediaElement m_MediaElement;

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
        public void Open(Uri uri)
        {
            lock (SyncLock)
            {
                Commands.Clear();
            }

            // Process the command in a background thread as opposed
            // to in the thread that it was called to prevent blocking.
            Runner.UIPumpInvoke(DispatcherPriority.Normal, () =>
            {
                var command = new OpenCommand(this, uri);
                command.ExecuteInternal();
            });
        }

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

        /// <summary>
        /// Seeks to the specified position within the media.
        /// </summary>
        /// <param name="position">The position.</param>
        public void Seek(TimeSpan position)
        {
            SeekCommand command = null;
            lock (SyncLock)
            {
                command = Commands.FirstOrDefault(c => c.CommandType == MediaCommandType.Seek) as SeekCommand;
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
        /// Closes the specified media.
        /// This command gets processed in a threadpool thread.
        /// </summary>
        public void Close()
        {
            lock (SyncLock)
            {
                Commands.Clear();
            }

            // Process the command in a background thread as opposed
            // to in the thread that it was called to prevent blocking.
            Runner.UIPumpInvoke(DispatcherPriority.Normal, () =>
            {
                var command = new CloseCommand(this);
                command.ExecuteInternal();
            });
        }

        /// <summary>
        /// Sets the playback speed ratio.
        /// </summary>
        /// <param name="targetSpeedRatio">The target speed ratio.</param>
        public void SetSpeedRatio(double targetSpeedRatio)
        {
            SpeedRatioCommand command = null;
            lock (SyncLock)
            {
                command = Commands.FirstOrDefault(c => c.CommandType == MediaCommandType.SetSpeedRatio) as SpeedRatioCommand;
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
        /// Waits for the command to complete execution.
        /// </summary>
        /// <param name="command">The command.</param>
        private void WaitFor(MediaCommand command)
        {
            while (command.HasCompleted == false && MediaElement.IsOpen)
                Runner.DoEvents(); 
        }

        #endregion

    }
}
