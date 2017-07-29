namespace Unosquare.FFME.Commands
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a command to be executed against an intance of the MediaElement
    /// </summary>
    internal abstract class MediaCommand
    {
        private TaskCompletionSource<bool> TaskCompleter;
        
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaCommand" /> class.
        /// </summary>
        /// <param name="manager">The command manager.</param>
        /// <param name="commandType">Type of the command.</param>
        protected MediaCommand(MediaCommandManager manager, MediaCommandType commandType)
        {
            Manager = manager;
            CommandType = commandType;
            TaskCompleter = new TaskCompletionSource<bool>();
            Promise = TaskCompleter.Task;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the associated parent command manager
        /// </summary>
        public MediaCommandManager Manager { get; private set; }

        /// <summary>
        /// Gets the type of the command.
        /// </summary>
        public MediaCommandType CommandType { get; private set; }

        /// <summary>
        /// Gets the promise-mode Task. You can wait for this task
        /// </summary>
        public Task Promise { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Executes this command asynchronously
        /// by starting the associated promise and awaiting it.
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task ExecuteAsync()
        {
            var m = Manager.MediaElement;

            // Avoid processing the command if the element is disposed.
            if (m.IsDisposed)
                return;

            if (m.Commands.ExecutingCommand != null)
                await m.Commands.ExecutingCommand.Promise;

            m.Commands.ExecutingCommand = this;
            Execute();
            TaskCompleter.TrySetResult(true);
            await Promise;
            m.Commands.ExecutingCommand = null;
        }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal abstract void Execute();

        #endregion
    }
}
