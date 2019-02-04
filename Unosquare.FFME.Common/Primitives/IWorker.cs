namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a standard API to control background application workers.
    /// </summary>
    /// <seealso cref="IDisposable" />
    public interface IWorker : IDisposable
    {
        /// <summary>
        /// Gets the current state of the worker.
        /// </summary>
        WorkerState WorkerState { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
        /// </value>
        bool IsDisposed { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is currently being disposed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is disposing; otherwise, <c>false</c>.
        /// </value>
        bool IsDisposing { get; }

        /// <summary>
        /// Gets or sets the time interval used to execute cycles.
        /// </summary>
        TimeSpan Period { get; set; }

        /// <summary>
        /// Gets the name identifier of this worker.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Sends an interrupt by canceling the execution token
        /// and immediately scheduling a new execution cycle without waiting.
        /// </summary>
        void Interrupt();

        /// <summary>
        /// Waits the specified milliseconds timeout for a cycle to complete.
        /// </summary>
        /// <param name="millisecondsTimeout">The milliseconds timeout.</param>
        /// <returns>True if the wait was successful</returns>
        bool Wait(int millisecondsTimeout);

        /// <summary>
        /// Starts execution of worker cycles.
        /// </summary>
        /// <returns>The awaitable task</returns>
        Task<WorkerState> StartAsync();

        /// <summary>
        /// Pauses execution of worker cycles.
        /// </summary>
        /// <param name="interrupt">if set to <c>true</c> an interrupt is sent to the worker.</param>
        /// <returns>The awaitable task</returns>
        Task<WorkerState> PauseAsync(bool interrupt);

        /// <summary>
        /// Resumes execution of worker cycles.
        /// </summary>
        /// <returns>The awaitable task</returns>
        Task<WorkerState> ResumeAsync();

        /// <summary>
        /// Permanently stops execution of worker cycles.
        /// An interrupt is always sent to the worker. If you wish to stop
        /// the worker without interrupting then call the <see cref="PauseAsync(bool)"/>
        /// method, await it, and finally call the <see cref="StopAsync"/> method.
        /// </summary>
        /// <returns>The awaitable task</returns>
        Task<WorkerState> StopAsync();
    }
}
