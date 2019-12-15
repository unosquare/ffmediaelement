namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using System;
    using System.Threading;
    using System.Windows;

    /// <summary>
    /// Represents a timer which performs an action on the UI thread when time elapses.  Rescheduling is supported.
    /// Original code from here: https://www.codeproject.com/Articles/32426/Deferring-ListCollectionView-filter-updates-for-a address.
    /// </summary>
    public sealed class DeferredAction : IDisposable
    {
        private readonly Timer DeferTimer;
        private bool IsDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeferredAction"/> class.
        /// </summary>
        /// <param name="action">The action.</param>
        private DeferredAction(Action<DeferredAction> action)
        {
            DeferTimer = new Timer(s => Application.Current?.Dispatcher?.Invoke(() => action(this)));
        }

        /// <summary>
        /// Creates a new DeferredAction.
        /// </summary>
        /// <param name="action">
        /// The action that will be deferred.  It is not performed until after <see cref="Defer"/> is called.
        /// </param>
        /// <returns>The Deferred Action.</returns>
        public static DeferredAction Create(Action<DeferredAction> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return new DeferredAction(action);
        }

        /// <summary>
        /// Defers performing the action until after time elapses.  Repeated calls will reschedule the action
        /// if it has not already been performed.
        /// </summary>
        /// <param name="delay">
        /// The amount of time to wait before performing the action.
        /// </param>
        public void Defer(TimeSpan delay)
        {
            // Fire action when time elapses (with no subsequent calls).
            DeferTimer.Change(delay, Timeout.InfiniteTimeSpan);
        }

        #region IDisposable Implementation

        /// <inheritdoc />
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            DeferTimer.Dispose();
        }

        #endregion
    }
}
