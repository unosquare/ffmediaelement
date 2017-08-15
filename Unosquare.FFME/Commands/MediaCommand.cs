namespace Unosquare.FFME.Commands
{
    using Core;
    using System.Threading;

    /// <summary>
    /// Represents a command to be executed against an intance of the MediaElement
    /// </summary>
    internal abstract class MediaCommand
    {
        /// <summary>
        /// Set when the command has finished execution.
        /// Do not use this field directly. It is managed internally by the command manager.
        /// </summary>
        private AtomicBoolean m_HasCompleted = new AtomicBoolean();

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
        /// Gets a value indicating whether this command is marked as completed.
        /// </summary>
        public bool HasCompleted
        {
            get { return m_HasCompleted.Value; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Marks the command as completed.
        /// </summary>
        public void Complete()
        {
            m_HasCompleted.Value = true;
        }

        /// <summary>
        /// Executes the code for the command
        /// </summary>
        public void Execute()
        {
            try
            {
                var m = Manager.MediaElement;

                // Avoid processing the command if the element is disposed.
                if (m.IsDisposed)
                    return;

                ExecuteInternal();
            }
            finally
            {
                Complete();
            }
        }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal abstract void ExecuteInternal();

        #endregion
    }
}
