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
        /// <summary>
        /// Enumerates the locking operations
        /// </summary>
        private enum LockHolderType
        {
            Read,
            Write,
        }

        /// <summary>
        /// Creates a reader-writer lock backed by a standard ReaderWriterLock
        /// </summary>
        /// <returns>The synchronized locker</returns>
        public static ISyncLocker CreateStandard()
        {
            return new StandardSyncLocker();
        }

        /// <summary>
        /// Creates a reader-writer lock backed by a ReaderWriterLockSlim
        /// </summary>
        /// <returns>The synchronized locker</returns>
        public static ISyncLocker CreateSlim()
        {
            return new SlimSyncLocker();
        }

        private sealed class StandardLockHolder : IDisposable
        {
            private bool IsDisposed = false;
            private StandardSyncLocker Parent = null;
            private LockHolderType Operation;

            public StandardLockHolder(StandardSyncLocker parent, LockHolderType operation)
            {
                Parent = parent;
                Operation = operation;
            }

            public void Dispose()
            {
                Dispose(true);
            }

            private void Dispose(bool alsoManaged)
            {
                if (!IsDisposed)
                {
                    if (alsoManaged)
                    {
                        if (Operation == LockHolderType.Read)
                            Parent.ReleaseReaderLock();
                        else
                            Parent.ReleaseWriterLock();
                    }

                    IsDisposed = true;
                }
            }
        }

        private sealed class StandardSyncLocker : ISyncLocker
        {
            private bool IsDisposed = false;
            private ReaderWriterLock Locker = new ReaderWriterLock();

            public IDisposable AcquireReaderLock()
            {
                Locker?.AcquireReaderLock(Timeout.Infinite);
                return new StandardLockHolder(this, LockHolderType.Read);
            }

            public IDisposable AcquireWriterLock()
            {
                Locker?.AcquireWriterLock(Timeout.Infinite);
                return new StandardLockHolder(this, LockHolderType.Write);
            }

            public void ReleaseWriterLock()
            {
                Locker?.ReleaseWriterLock();
            }

            public void ReleaseReaderLock()
            {
                Locker?.ReleaseReaderLock();
            }

            public void Dispose()
            {
                Dispose(true);
            }

            private void Dispose(bool alsoManaged)
            {
                if (!IsDisposed)
                {
                    if (alsoManaged)
                    {
                        Locker?.ReleaseLock();
                    }

                    Locker = null;
                    IsDisposed = true;
                }
            }
        }

        private sealed class SlimLockHolder : IDisposable
        {
            private bool IsDisposed = false;
            private SlimSyncLocker Parent = null;
            private LockHolderType Operation;

            public SlimLockHolder(SlimSyncLocker parent, LockHolderType operation)
            {
                Parent = parent;
                Operation = operation;
            }

            public void Dispose()
            {
                Dispose(true);
            }

            private void Dispose(bool alsoManaged)
            {
                if (!IsDisposed)
                {
                    if (alsoManaged)
                    {
                        if (Operation == LockHolderType.Read)
                            Parent.ReleaseReaderLock();
                        else
                            Parent.ReleaseWriterLock();
                    }

                    IsDisposed = true;
                }
            }
        }

        private sealed class SlimSyncLocker : ISyncLocker
        {
            private bool IsDisposed = false;
            private ReaderWriterLockSlim Locker = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

            public IDisposable AcquireReaderLock()
            {
                Locker?.EnterReadLock();
                return new SlimLockHolder(this, LockHolderType.Read);
            }

            public IDisposable AcquireWriterLock()
            {
                Locker?.EnterWriteLock();
                return new SlimLockHolder(this, LockHolderType.Write);
            }

            public void ReleaseWriterLock()
            {
                Locker?.ExitWriteLock();
            }

            public void ReleaseReaderLock()
            {
                Locker?.ExitReadLock();
            }

            public void Dispose()
            {
                Dispose(true);
            }

            private void Dispose(bool alsoManaged)
            {
                if (!IsDisposed)
                {
                    if (alsoManaged)
                    {
                        Locker?.Dispose();
                    }

                    Locker = null;
                    IsDisposed = true;
                }
            }
        }
    }
}
