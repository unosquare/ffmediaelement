namespace Unosquare.FFME.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a command to be executed against an intance of the MediaElement
    /// </summary>
    internal abstract class MediaCommand : IDisposable
    {
        #region State Variables
        
        private bool IsDisposed = false; // To detect redundant calls
        private CancellationTokenSource CancelTokenSource = new CancellationTokenSource();

        #endregion

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
        public bool HasCompleted => IsDisposed || TaskContext.IsCompleted;

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
            if (IsDisposed) return;

            // Signal the cancellation
            CancelTokenSource.Cancel();
        }

        /// <summary>
        /// Executes the code for the command asynchronously
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task StartAsync()
        {
            var m = Manager.MediaElement;

            // Avoid processing the command if the element is disposed.
            if (IsDisposed || m.IsDisposed)
                return;

            // Start and await the task
            try
            {
                IsRunning = true;
                TaskContext.Start();
                await TaskContext.ContinueWith(a => { Dispose(); });
            }
            catch
            {
                throw;
            }
            finally
            {
                IsRunning = false;
            }
        }

        /// <summary>
        /// Executes the command Synchronously.
        /// </summary>
        public void RunSynchronously()
        {
            StartAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
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

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool alsoManaged)
        {
            if (!IsDisposed)
            {
                // Set the disposed flag to true
                IsDisposed = true;

                if (alsoManaged)
                {
                    TaskContext?.Dispose();
                    CancelTokenSource?.Dispose();
                }

                // free unmanaged resources and set fields to null;
                TaskContext = null;
                CancelTokenSource = null;
            }
        }

        #endregion
    }
}
