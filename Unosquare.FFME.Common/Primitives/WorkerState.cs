namespace Unosquare.FFME.Primitives
{
    /// <summary>
    /// Enumerates the different states in which a worker can be.
    /// </summary>
    public enum WorkerState
    {
        /// <summary>
        /// The worker has been created and it is ready to start.
        /// </summary>
        Created,

        /// <summary>
        /// The worker is running it cycle logic.
        /// </summary>
        Running,

        /// <summary>
        /// The worker is running its delay logic.
        /// </summary>
        Waiting,

        /// <summary>
        /// The worker is in the paused or suspended state.
        /// </summary>
        Paused,

        /// <summary>
        /// The worker is stopped and ready for disposal.
        /// </summary>
        Stopped
    }
}
