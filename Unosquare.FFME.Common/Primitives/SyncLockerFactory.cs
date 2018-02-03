namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Threading;

    /// <summary>
    /// Provides factory methods to create synchronized reader-writer locks
    /// that support a generalized locking and releasing api and syntax.
    /// </summary>
    public static class SyncLockerFactory
    {
        #region Enums and Interfaces

        /// <summary>
        /// Enumerates the locking operations
        /// </summary>
        private enum LockHolderType
        {
            Read,
            Write,
        }

        /// <summary>
        /// Defines methods for releasing locks
        /// </summary>
        private interface ISyncReleasable
        {
            /// <summary>
            /// Releases the writer lock.
            /// </summary>
            void ReleaseWriterLock();

            /// <summary>
            /// Releases the reader lock.
            /// </summary>
            void ReleaseReaderLock();
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Creates a reader-writer lock backed by a standard ReaderWriterLock
        /// </summary>
        /// <returns>The synchronized locker</returns>
        public static ISyncLocker Create() => new SyncLocker();

        /// <summary>
        /// Creates a reader-writer lock backed by a ReaderWriterLockSlim
        /// </summary>
        /// <returns>The synchronized locker</returns>
        public static ISyncLocker CreateSlim() => new SyncLockerSlim();

        /// <summary>
        /// Creates a reader-writer lock.
        /// </summary>
        /// <param name="useSlim">if set to <c>true</c> it uses the Slim version of a reader-writer lock.</param>
        /// <returns>The Sync Locker</returns>
        public static ISyncLocker Create(bool useSlim) => useSlim ? CreateSlim() : Create();

        #endregion

        #region Private Classes

        /// <summary>
        /// The lock releaser. Calling the dispose method releases the lock entered by the parent SyncLocker.
        /// </summary>
        /// <seealso cref="System.IDisposable" />
        private sealed class SyncLockReleaser : IDisposable
        {
            private bool IsDisposed = false;
            private ISyncReleasable Parent = null;
            private LockHolderType Operation;

            /// <summary>
            /// Initializes a new instance of the <see cref="SyncLockReleaser"/> class.
            /// </summary>
            /// <param name="parent">The parent.</param>
            /// <param name="operation">The operation.</param>
            public SyncLockReleaser(ISyncReleasable parent, LockHolderType operation)
            {
                Parent = parent;
                Operation = operation;
            }

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose() => Dispose(true);

            /// <summary>
            /// Releases unmanaged and - optionally - managed resources.
            /// </summary>
            /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
            private void Dispose(bool alsoManaged)
            {
                if (IsDisposed) return;
                IsDisposed = true;

                if (Operation == LockHolderType.Read)
                    Parent.ReleaseReaderLock();
                else
                    Parent.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// The Sync Locker backed by a ReaderWriterLock
        /// </summary>
        /// <seealso cref="ISyncLocker" />
        /// <seealso cref="ISyncReleasable" />
        private sealed class SyncLocker : ISyncLocker, ISyncReleasable
        {
            private bool IsDisposed = false;
            private ReaderWriterLock Locker = new ReaderWriterLock();

            /// <summary>
            /// Acquires a reader lock.
            /// The lock is released when the returned locking object is disposed.
            /// </summary>
            /// <returns>
            /// A disposable locking object.
            /// </returns>
            public IDisposable AcquireReaderLock()
            {
                Locker?.AcquireReaderLock(Timeout.Infinite);
                return new SyncLockReleaser(this, LockHolderType.Read);
            }

            /// <summary>
            /// Acquires a writer lock.
            /// The lock is released when the returned locking object is disposed.
            /// </summary>
            /// <returns>
            /// A disposable locking object.
            /// </returns>
            public IDisposable AcquireWriterLock()
            {
                Locker?.AcquireWriterLock(Timeout.Infinite);
                return new SyncLockReleaser(this, LockHolderType.Write);
            }

            /// <summary>
            /// Releases the writer lock.
            /// </summary>
            public void ReleaseWriterLock() => Locker?.ReleaseWriterLock();

            /// <summary>
            /// Releases the reader lock.
            /// </summary>
            public void ReleaseReaderLock() => Locker?.ReleaseReaderLock();

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose() => Dispose(true);

            /// <summary>
            /// Releases unmanaged and - optionally - managed resources.
            /// </summary>
            /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
            private void Dispose(bool alsoManaged)
            {
                if (IsDisposed) return;
                IsDisposed = true;
                Locker?.ReleaseLock();
                Locker = null;
            }
        }

        /// <summary>
        /// The Sync Locker backed by ReaderWriterLockSlim
        /// </summary>
        /// <seealso cref="ISyncLocker" />
        /// <seealso cref="ISyncReleasable" />
        private sealed class SyncLockerSlim : ISyncLocker, ISyncReleasable
        {
            private bool IsDisposed = false;
            private ReaderWriterLockSlim Locker
                = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

            /// <summary>
            /// Acquires a reader lock.
            /// The lock is released when the returned locking object is disposed.
            /// </summary>
            /// <returns>
            /// A disposable locking object.
            /// </returns>
            public IDisposable AcquireReaderLock()
            {
                Locker?.EnterReadLock();
                return new SyncLockReleaser(this, LockHolderType.Read);
            }

            /// <summary>
            /// Acquires a writer lock.
            /// The lock is released when the returned locking object is disposed.
            /// </summary>
            /// <returns>
            /// A disposable locking object.
            /// </returns>
            public IDisposable AcquireWriterLock()
            {
                Locker?.EnterWriteLock();
                return new SyncLockReleaser(this, LockHolderType.Write);
            }

            /// <summary>
            /// Releases the writer lock.
            /// </summary>
            public void ReleaseWriterLock() => Locker?.ExitWriteLock();

            /// <summary>
            /// Releases the reader lock.
            /// </summary>
            public void ReleaseReaderLock() => Locker?.ExitReadLock();

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose() => Dispose(true);

            /// <summary>
            /// Releases unmanaged and - optionally - managed resources.
            /// </summary>
            /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
            private void Dispose(bool alsoManaged)
            {
                if (IsDisposed) return;
                IsDisposed = true;
                Locker?.Dispose();
                Locker = null;
            }
        }

        #endregion
    }
}
