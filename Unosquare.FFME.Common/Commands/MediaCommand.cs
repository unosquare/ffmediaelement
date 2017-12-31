namespace Unosquare.FFME.Commands
{
    using Core;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a command to be executed against an intance of the MediaElement
    /// </summary>
    internal abstract class MediaCommand
    {
        private CancellationTokenSource CancelTokenSource = new CancellationTokenSource();

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
            TaskContext = new Task(ExecuteInternal, CancelTokenSource.Token);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the associated parent command manager
        /// </summary>
        public MediaCommandManager Manager { get; }

        /// <summary>
        /// Gets the type of the command.
        /// </summary>
        public MediaCommandType CommandType { get; }

        /// <summary>
        /// Gets a value indicating whether this command is marked as completed.
        /// </summary>
        public bool HasCompleted => TaskContext.IsCompleted;

        /// <summary>
        /// Gets the task that this command will run.
        /// </summary>
        public Task TaskContext { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance is running.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is running; otherwise, <c>false</c>.
        /// </value>
        public bool IsRunning { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Marks the command as completed.
        /// </summary>
        public void Complete()
        {
            CancelTokenSource.Cancel();
        }

        /// <summary>
        /// Executes the code for the command
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task ExecuteAsync()
        {
            var m = Manager.MediaElement;

            // Avoid processing the command if the element is disposed.
            if (m.IsDisposed)
                return;

            // Start and await the task
            try
            {
                IsRunning = true;
                TaskContext.Start();
                await TaskContext;
            }
            finally
            {
                IsRunning = false;
            }
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"{CommandType} - ID: {TaskContext.Id} Canceled: {TaskContext.IsCanceled}; Completed: {TaskContext.IsCompleted}; Status: {TaskContext.Status}; State: {TaskContext.AsyncState}";
        }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal abstract void ExecuteInternal();

        #endregion
    }
}
