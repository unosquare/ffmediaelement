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

        private const int DefaultTimeout = 100;

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
        /// <seealso cref="IDisposable" />
        private sealed class SyncLockReleaser : IDisposable
        {
            private readonly ISyncReleasable Parent = null;
            private readonly LockHolderType Operation;
            private bool IsDisposed;

            /// <summary>
            /// Initializes a new instance of the <see cref="SyncLockReleaser"/> class.
            /// </summary>
            /// <param name="parent">The parent.</param>
            /// <param name="operation">The operation.</param>
            public SyncLockReleaser(ISyncReleasable parent, LockHolderType operation)
            {
                Parent = parent;
                Operation = operation;

                if (parent == null)
                    IsDisposed = true;
            }

            /// <summary>
            /// An actionless, dummy disposable object.
            /// </summary>
            public static SyncLockReleaser Empty { get; } = new SyncLockReleaser(null, default);

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
                    Parent?.ReleaseReaderLock();
                else
                    Parent?.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// The Sync Locker backed by a ReaderWriterLock
        /// </summary>
        /// <seealso cref="ISyncLocker" />
        /// <seealso cref="ISyncReleasable" />
        private sealed class SyncLocker : ISyncLocker, ISyncReleasable
        {
            private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);
            private readonly ReaderWriterLock Locker = new ReaderWriterLock();

            /// <summary>
            /// Gets a value indicating whether this instance is disposed.
            /// </summary>
            public bool IsDisposed => m_IsDisposed.Value;

            /// <summary>
            /// Acquires a reader lock.
            /// The lock is released when the returned locking object is disposed.
            /// </summary>
            /// <returns>
            /// A disposable locking object.
            /// </returns>
            public IDisposable AcquireReaderLock()
            {
                AcquireReaderLock(Timeout.Infinite, out var releaser);
                return releaser;
            }

            /// <summary>
            /// Tries to acquire a reader lock with a timeout.
            /// </summary>
            /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
            /// <param name="locker">The locker.</param>
            /// <returns>
            /// True if the lock was acquired
            /// </returns>
            public bool TryAcquireReaderLock(int timeoutMilliseconds, out IDisposable locker) =>
                AcquireReaderLock(timeoutMilliseconds, out locker);

            /// <summary>
            /// Acquires a writer lock.
            /// The lock is released when the returned locking object is disposed.
            /// </summary>
            /// <returns>
            /// A disposable locking object.
            /// </returns>
            public IDisposable AcquireWriterLock()
            {
                AcquireWriterLock(Timeout.Infinite, out var releaser);
                return releaser;
            }

            /// <summary>
            /// Tries to acquire a writer lock with a tiemout.
            /// </summary>
            /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
            /// <param name="locker">The locker.</param>
            /// <returns>
            /// True if the lock was acquired
            /// </returns>
            public bool TryAcquireWriterLock(int timeoutMilliseconds, out IDisposable locker) =>
                AcquireWriterLock(timeoutMilliseconds, out locker);

            /// <summary>
            /// Tries to acquire a writer lock with a default tiemout.
            /// </summary>
            /// <param name="locker">The locker.</param>
            /// <returns>
            /// True if the lock was acquired
            /// </returns>
            public bool TryAcquireWriterLock(out IDisposable locker) =>
                TryAcquireWriterLock(DefaultTimeout, out locker);

            /// <summary>
            /// Tries to acquire a reader lock with a default timeout.
            /// </summary>
            /// <param name="locker">The locker.</param>
            /// <returns>
            /// True if the lock was acquired
            /// </returns>
            public bool TryAcquireReaderLock(out IDisposable locker) =>
                TryAcquireReaderLock(DefaultTimeout, out locker);

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
            /// Acquires the writer lock.
            /// </summary>
            /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
            /// <param name="releaser">The releaser.</param>
            /// <returns>Success</returns>
            private bool AcquireWriterLock(int timeoutMilliseconds, out IDisposable releaser)
            {
                if (m_IsDisposed == true) throw new ObjectDisposedException(nameof(ISyncLocker));

                releaser = SyncLockReleaser.Empty;
                if (Locker.IsReaderLockHeld)
                {
                    Locker.AcquireReaderLock(timeoutMilliseconds);
                    releaser = new SyncLockReleaser(this, LockHolderType.Read);
                    return Locker?.IsReaderLockHeld ?? false;
                }

                Locker.AcquireWriterLock(timeoutMilliseconds);
                if (Locker?.IsWriterLockHeld ?? false)
                {
                    releaser = new SyncLockReleaser(this, LockHolderType.Write);
                }

                return Locker?.IsWriterLockHeld ?? false;
            }

            /// <summary>
            /// Acquires the reader lock.
            /// </summary>
            /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
            /// <param name="releaser">The releaser.</param>
            /// <returns>Success</returns>
            private bool AcquireReaderLock(int timeoutMilliseconds, out IDisposable releaser)
            {
                if (m_IsDisposed == true) throw new ObjectDisposedException(nameof(ISyncLocker));

                releaser = SyncLockReleaser.Empty;
                Locker.AcquireReaderLock(timeoutMilliseconds);
                if (Locker.IsReaderLockHeld)
                {
                    releaser = new SyncLockReleaser(this, LockHolderType.Read);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Releases unmanaged and - optionally - managed resources.
            /// </summary>
            /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
            private void Dispose(bool alsoManaged)
            {
                if (m_IsDisposed == true) return;
                m_IsDisposed.Value = true;
                Locker.ReleaseLock();
            }
        }

        /// <summary>
        /// The Sync Locker backed by ReaderWriterLockSlim
        /// </summary>
        /// <seealso cref="ISyncLocker" />
        /// <seealso cref="ISyncReleasable" />
        private sealed class SyncLockerSlim : ISyncLocker, ISyncReleasable
        {
            private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);
            private readonly ReaderWriterLockSlim Locker
                = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

            /// <summary>
            /// Gets a value indicating whether this instance is disposed.
            /// </summary>
            public bool IsDisposed => m_IsDisposed.Value;

            /// <summary>
            /// Acquires a reader lock.
            /// The lock is released when the returned locking object is disposed.
            /// </summary>
            /// <returns>
            /// A disposable locking object.
            /// </returns>
            public IDisposable AcquireReaderLock()
            {
                AcquireReaderLock(Timeout.Infinite, out var releaser);
                return releaser;
            }

            /// <summary>
            /// Tries to acquire a reader lock with a timeout.
            /// </summary>
            /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
            /// <param name="locker">The locker.</param>
            /// <returns>
            /// True if the lock was acquired
            /// </returns>
            public bool TryAcquireReaderLock(int timeoutMilliseconds, out IDisposable locker) =>
                AcquireReaderLock(timeoutMilliseconds, out locker);

            /// <summary>
            /// Acquires a writer lock.
            /// The lock is released when the returned locking object is disposed.
            /// </summary>
            /// <returns>
            /// A disposable locking object.
            /// </returns>
            public IDisposable AcquireWriterLock()
            {
                AcquireWriterLock(Timeout.Infinite, out var releaser);
                return releaser;
            }

            /// <summary>
            /// Tries to acquire a writer lock with a tiemout.
            /// </summary>
            /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
            /// <param name="locker">The locker.</param>
            /// <returns>
            /// True if the lock was acquired
            /// </returns>
            public bool TryAcquireWriterLock(int timeoutMilliseconds, out IDisposable locker) =>
                AcquireWriterLock(timeoutMilliseconds, out locker);

            /// <summary>
            /// Tries to acquire a writer lock with a default tiemout.
            /// </summary>
            /// <param name="locker">The locker.</param>
            /// <returns>
            /// True if the lock was acquired
            /// </returns>
            public bool TryAcquireWriterLock(out IDisposable locker) =>
                TryAcquireWriterLock(DefaultTimeout, out locker);

            /// <summary>
            /// Tries to acquire a reader lock with a default timeout.
            /// </summary>
            /// <param name="locker">The locker.</param>
            /// <returns>
            /// True if the lock was acquired
            /// </returns>
            public bool TryAcquireReaderLock(out IDisposable locker) =>
                TryAcquireReaderLock(DefaultTimeout, out locker);

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
            /// Acquires the writer lock.
            /// </summary>
            /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
            /// <param name="releaser">The releaser.</param>
            /// <returns>Success</returns>
            private bool AcquireWriterLock(int timeoutMilliseconds, out IDisposable releaser)
            {
                if (m_IsDisposed == true) throw new ObjectDisposedException(nameof(ISyncLocker));

                releaser = SyncLockReleaser.Empty;
                var result = false;

                if (Locker?.IsReadLockHeld ?? false)
                {
                    result = Locker?.TryEnterReadLock(timeoutMilliseconds) ?? false;
                    if (result)
                        releaser = new SyncLockReleaser(this, LockHolderType.Read);

                    return result;
                }

                result = Locker?.TryEnterWriteLock(timeoutMilliseconds) ?? false;
                if (result)
                    releaser = new SyncLockReleaser(this, LockHolderType.Write);

                return result;
            }

            /// <summary>
            /// Acquires the reader lock.
            /// </summary>
            /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
            /// <param name="releaser">The releaser.</param>
            /// <returns>Success</returns>
            private bool AcquireReaderLock(int timeoutMilliseconds, out IDisposable releaser)
            {
                if (m_IsDisposed == true) throw new ObjectDisposedException(nameof(ISyncLocker));

                releaser = SyncLockReleaser.Empty;
                var result = Locker?.TryEnterReadLock(timeoutMilliseconds) ?? false;
                if (result)
                    releaser = new SyncLockReleaser(this, LockHolderType.Read);

                return result;
            }

            /// <summary>
            /// Releases unmanaged and - optionally - managed resources.
            /// </summary>
            /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
            private void Dispose(bool alsoManaged)
            {
                if (m_IsDisposed == true) return;
                m_IsDisposed.Value = true;
                Locker.Dispose();
            }
        }

        #endregion
    }
}
