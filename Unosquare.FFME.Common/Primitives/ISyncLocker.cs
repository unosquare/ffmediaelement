namespace Unosquare.FFME.Primitives
{
    using System;

    /// <summary>
    /// Defines a generic interface for synchronized locking mechanisms
    /// </summary>
    public interface ISyncLocker : IDisposable
    {
        /// <summary>
        /// Acquires a writer lock.
        /// The lock is released when the returned locking object is disposed.
        /// </summary>
        /// <returns>A disposable locking object.</returns>
        IDisposable AcquireWriterLock();

        /// <summary>
        /// Tries to acquire a writer lock with a tiemout.
        /// </summary>
        /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
        /// <param name="locker">The locker.</param>
        /// <returns>True if the lock was acquired</returns>
        bool TryAcquireWriterLock(int timeoutMilliseconds, out IDisposable locker);

        /// <summary>
        /// Tries to acquire a writer lock with a default tiemout.
        /// </summary>
        /// <param name="locker">The locker.</param>
        /// /// <returns>True if the lock was acquired</returns>
        bool TryAcquireWriterLock(out IDisposable locker);

        /// <summary>
        /// Acquires a reader lock.
        /// The lock is released when the returned locking object is disposed.
        /// </summary>
        /// <returns>A disposable locking object.</returns>
        IDisposable AcquireReaderLock();

        /// <summary>
        /// Tries to acquire a reader lock with a timeout.
        /// </summary>
        /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
        /// <param name="locker">The locker.</param>
        /// <returns>True if the lock was acquired</returns>
        bool TryAcquireReaderLock(int timeoutMilliseconds, out IDisposable locker);

        /// <summary>
        /// Tries to acquire a reader lock with a default timeout.
        /// </summary>
        /// <param name="locker">The locker.</param>
        /// <returns>True if the lock was acquired</returns>
        bool TryAcquireReaderLock(out IDisposable locker);
    }
}
