namespace Unosquare.FFME.Primitives
{
    using System;

    /// <summary>
    /// Provides a generalized API for ManualResetEvent and ManualResetEventSlim
    /// </summary>
    /// <seealso cref="IDisposable" />
    public interface IWaitEvent : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether the event is in the completed state.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Gets a value indicating whether the Begin method has been called.
        /// It returns false after the Complete method is called
        /// </summary>
        bool IsInProgress { get; }

        /// <summary>
        /// Returns true if the underlying handle is not closed and it is still valid.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Enters the state in which waiters need to wait.
        /// All future waiters will block when they call the Wait method
        /// </summary>
        void Begin();

        /// <summary>
        /// Leaves the state in which waiters need to wait.
        /// All current waiters will continue.
        /// </summary>
        void Complete();

        /// <summary>
        /// Waits for the event to be completed
        /// </summary>
        void Wait();

        /// <summary>
        /// Waits for the event to be completed.
        /// Returns True when there was no timeout. False if the tiemout was reached
        /// </summary>
        /// <param name="timeout">The maximum amount of time to wait for.</param>
        /// <returns>True when there was no timeout. False if the tiemout was reached</returns>
        bool Wait(TimeSpan timeout);
    }
}
