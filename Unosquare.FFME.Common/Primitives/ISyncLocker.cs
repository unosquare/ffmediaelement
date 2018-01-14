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
        /// Acquires a reader lock.
        /// The lock is released when the returned locking object is disposed.
        /// </summary>
        /// <returns>A disposable locking object.</returns>
        IDisposable AcquireReaderLock();
    }
}
