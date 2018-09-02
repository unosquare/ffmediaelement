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

        /// <inheritdoc />
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

            /// <inheritdoc />
            public bool IsDisposed
            {
                get => m_IsDisposed.Value;
                private set => m_IsDisposed.Value = value;
            }

            /// <inheritdoc />
            public bool IsValid
            {
                get
                {
                    if (IsDisposed) return false;
                    if (Event.SafeWaitHandle?.IsClosed ?? true) return false;
                    return !(Event.SafeWaitHandle?.IsInvalid ?? true);
                }
            }

            /// <inheritdoc />
            public bool IsCompleted => IsValid == false || Event.WaitOne(0);

            /// <inheritdoc />
            public bool IsInProgress => !IsCompleted;

            /// <inheritdoc />
            public void Begin() { if (IsDisposed) Event.Reset(); }

            /// <inheritdoc />
            public void Complete() { if (IsDisposed) Event.Set(); }

            /// <inheritdoc />
            public void Dispose()
            {
                if (IsDisposed) return;
                IsDisposed = true;

                Event.Set();
                Event.Dispose();
            }

            /// <inheritdoc />
            public void Wait() { if (IsDisposed) Event.WaitOne(); }

            /// <inheritdoc />
            public bool Wait(TimeSpan timeout) => IsDisposed || Event.WaitOne(timeout);
        }

        /// <inheritdoc />
        /// <summary>
        /// Defines a WaitEvent backed by a ManualResetEventSlim
        /// </summary>
        /// <seealso cref="IWaitEvent" />
        private class WaitEventSlim : IWaitEvent
        {
            private readonly AtomicBoolean m_IsDisposed = new AtomicBoolean(false);
            private readonly ManualResetEventSlim Event;

            /// <summary>
            /// Initializes a new instance of the <see cref="WaitEventSlim"/> class.
            /// </summary>
            /// <param name="isCompleted">if set to <c>true</c> [is completed].</param>
            public WaitEventSlim(bool isCompleted)
            {
                Event = new ManualResetEventSlim(isCompleted);
            }

            /// <inheritdoc />
            public bool IsDisposed
            {
                get => m_IsDisposed.Value;
                private set => m_IsDisposed.Value = value;
            }

            /// <inheritdoc />
            public bool IsValid
            {
                get
                {
                    if (IsDisposed) return false;
                    if (Event.WaitHandle == null) return false;
                    return Event.WaitHandle.SafeWaitHandle == null ||
                           (!Event.WaitHandle.SafeWaitHandle.IsClosed && !Event.WaitHandle.SafeWaitHandle.IsInvalid);
                }
            }

            /// <inheritdoc />
            public bool IsCompleted => IsValid == false || Event.IsSet;

            /// <inheritdoc />
            public bool IsInProgress => !IsCompleted;

            /// <inheritdoc />
            public void Begin() { if (IsDisposed == false) Event.Reset(); }

            /// <inheritdoc />
            public void Complete() { if (IsDisposed == false) Event.Set(); }

            /// <inheritdoc />
            public void Dispose()
            {
                if (IsDisposed) return;
                IsDisposed = true;

                Event.Set();
                Event.Dispose();
            }

            /// <inheritdoc />
            public void Wait() { if (IsDisposed == false) Event.Wait(); }

            /// <inheritdoc />
            public bool Wait(TimeSpan timeout) => IsDisposed || Event.Wait(timeout);
        }

        #endregion
    }
}
