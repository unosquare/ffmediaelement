namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Threading;

    /// <summary>
    /// Provides a Manual Reset Event factory with a unified API
    /// </summary>
    public static class WaitEventFactory
    {
        #region Factory Methods

        /// <summary>
        /// Creates a Wait Event backed by a standard ManualResetEvent
        /// </summary>
        /// <param name="isCompleted">if initially set to completed. Generally true</param>
        /// <returns>The Wait Event</returns>
        public static IWaitEvent Create(bool isCompleted) => new WaitEvent(isCompleted);

        /// <summary>
        /// Creates a Wait Event backed by a ManualResetEventSlim
        /// </summary>
        /// <param name="isCompleted">if initially set to completed. Generally true</param>
        /// <returns>The Wait Event</returns>
        public static IWaitEvent CreateSlim(bool isCompleted) => new WaitEventSlim(isCompleted);

        /// <summary>
        /// Creates a Wait Event backed by a ManualResetEventSlim
        /// </summary>
        /// <param name="isCompleted">if initially set to completed. Generally true</param>
        /// <param name="useSlim">if set to <c>true</c> creates a slim version of the wait event.</param>
        /// <returns>The Wait Event</returns>
        public static IWaitEvent Create(bool isCompleted, bool useSlim) => useSlim ? CreateSlim(isCompleted) : Create(isCompleted);

        #endregion

        #region Backing Classes

        /// <summary>
        /// Defines a WaitEvent backed by a ManualResetEvent
        /// </summary>
        /// <seealso cref="IWaitEvent" />
        private class WaitEvent : IWaitEvent
        {
            private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);
            private readonly ManualResetEvent Event;

            /// <summary>
            /// Initializes a new instance of the <see cref="WaitEvent"/> class.
            /// </summary>
            /// <param name="isCompleted">if set to <c>true</c> [is completed].</param>
            public WaitEvent(bool isCompleted)
            {
                Event = new ManualResetEvent(isCompleted);
            }

            /// <summary>
            /// Gets a value indicating whether this instance is disposed.
            /// </summary>
            public bool IsDisposed
            {
                get => m_IsDisposed.Value;
                private set => m_IsDisposed.Value = value;
            }

            /// <summary>
            /// Returns true if the underlying handle is not closed and it is still valid.
            /// </summary>
            public bool IsValid
            {
                get
                {
                    if (IsDisposed) return false;
                    if (Event.SafeWaitHandle?.IsClosed ?? true) return false;
                    if (Event.SafeWaitHandle?.IsInvalid ?? true) return false;
                    return true;
                }
            }

            /// <summary>
            /// Gets a value indicating whether this instance is done.
            /// </summary>
            public bool IsCompleted
            {
                get
                {
                    if (IsValid == false) return true;
                    return Event.WaitOne(0);
                }
            }

            /// <summary>
            /// Gets a value indicating whether the Begin method has been called.
            /// It returns false after the Complete method is called
            /// </summary>
            public bool IsInProgress => !IsCompleted;

            /// <summary>
            /// Enters the state in which waiters need to wait.
            /// All future waiters will block when they call the Wait method
            /// </summary>
            public void Begin() { if (IsDisposed) Event.Reset(); }

            /// <summary>
            /// Leaves the state in which waiters need to wait.
            /// All current waiters will continue.
            /// </summary>
            public void Complete() { if (IsDisposed) Event.Set(); }

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose()
            {
                if (IsDisposed) return;
                IsDisposed = true;

                Event.Set();
                Event.Dispose();
            }

            /// <summary>
            /// Waits for the event to be completed
            /// </summary>
            public void Wait() { if (IsDisposed) Event.WaitOne(); }

            /// <summary>
            /// Waits for the event to be completed.
            /// Returns True when there was no timeout. False if the tiemout was reached
            /// </summary>
            /// <param name="timeout">The maximum amount of time to wait for.</param>
            /// <returns>
            /// True when there was no timeout. False if the tiemout was reached
            /// </returns>
            public bool Wait(TimeSpan timeout) => IsDisposed == false ? Event.WaitOne(timeout) : true;
        }

        /// <summary>
        /// Defines a WaitEvent backed by a ManualResetEventSlim
        /// </summary>
        /// <seealso cref="IWaitEvent" />
        private class WaitEventSlim : IWaitEvent
        {
            private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);
            private readonly ManualResetEventSlim Event = null;

            /// <summary>
            /// Initializes a new instance of the <see cref="WaitEventSlim"/> class.
            /// </summary>
            /// <param name="isCompleted">if set to <c>true</c> [is completed].</param>
            public WaitEventSlim(bool isCompleted)
            {
                Event = new ManualResetEventSlim(isCompleted);
            }

            /// <summary>
            /// Gets a value indicating whether this instance is disposed.
            /// </summary>
            public bool IsDisposed
            {
                get => m_IsDisposed.Value;
                private set => m_IsDisposed.Value = value;
            }

            /// <summary>
            /// Returns true if the underlying handle is not closed and it is still valid.
            /// </summary>
            public bool IsValid
            {
                get
                {
                    if (IsDisposed) return false;
                    if (Event.WaitHandle == null) return false;
                    if (Event.WaitHandle.SafeWaitHandle != null
                        && (Event.WaitHandle.SafeWaitHandle.IsClosed || Event.WaitHandle.SafeWaitHandle.IsInvalid))
                    {
                        return false;
                    }

                    return true;
                }
            }

            /// <summary>
            /// Gets a value indicating whether this instance is done.
            /// </summary>
            public bool IsCompleted
            {
                get
                {
                    if (IsValid == false) return true;
                    return Event.IsSet;
                }
            }

            /// <summary>
            /// Gets a value indicating whether the Begin method has been called.
            /// It returns false after the Complete method is called
            /// </summary>
            public bool IsInProgress => !IsCompleted;

            /// <summary>
            /// Enters the state in which waiters need to wait.
            /// All future waiters will block when they call the Wait method
            /// </summary>
            public void Begin() { if (IsDisposed == false) Event.Reset(); }

            /// <summary>
            /// Leaves the state in which waiters need to wait.
            /// All current waiters will continue.
            /// </summary>
            public void Complete() { if (IsDisposed == false) Event.Set(); }

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose()
            {
                if (IsDisposed) return;
                IsDisposed = true;

                Event.Set();
                Event.Dispose();
            }

            /// <summary>
            /// Waits for the event to be completed
            /// </summary>
            public void Wait() { if (IsDisposed == false) Event.Wait(); }

            /// <summary>
            /// Waits for the event to be completed.
            /// Returns True when there was no timeout. False if the tiemout was reached
            /// </summary>
            /// <param name="timeout">The maximum amount of time to wait for.</param>
            /// <returns>
            /// True when there was no timeout. False if the tiemout was reached
            /// </returns>
            public bool Wait(TimeSpan timeout) => IsDisposed == false ? Event.Wait(timeout) : true;
        }

        #endregion
    }
}
