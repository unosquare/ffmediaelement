namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Threading;

    /// <summary>
    /// Defines a timer for discrete intervals.
    /// It provides access to high resolution time quanta when available.
    /// </summary>
    internal sealed class IntervalTimer : IDisposable
    {
        private readonly Action UserCallback;

        private readonly int ThreadingTimerInterval = 16;
        private readonly int MultimediaTimerInterval;

        private readonly Timer ThreadingTimer;
        private IDisposable MultimediaTimer;

        private int m_IsDisposing;
        private int m_IsRunningCycle;
        private int m_ActiveTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntervalTimer"/> class.
        /// </summary>
        /// <param name="useMultimediaTimer">if set to <c>true</c> [use multimedia timer].</param>
        /// <param name="callback">The callback.</param>
        public IntervalTimer(bool useMultimediaTimer, Action callback)
        {
            UserCallback = callback;
            ThreadingTimer = new Timer(ThreadingTimerCallback, null, Timeout.Infinite, ThreadingTimerInterval);
            MultimediaTimerInterval = NativeTiming.IsAvailable ? NativeTiming.MinResolutionPeriod : 0;
            IsMultimedia = useMultimediaTimer;
        }

        /// <summary>
        /// Defines the different timer types.
        /// </summary>
        private enum TimerType
        {
            /// <summary>
            /// No active timer.
            /// </summary>
            None,

            /// <summary>
            /// The standard threading timer.
            /// </summary>
            Threading,

            /// <summary>
            /// The multimedia timer.
            /// </summary>
            Multimedia,
        }

        /// <summary>
        /// Gets or sets a value indicating whether this timer is multimedia (high precision) based.
        /// </summary>
        public bool IsMultimedia
        {
            get
            {
                return ActiveTimer == TimerType.Multimedia;
            }
            set
            {
                ActiveTimer = TimerType.None;
                var enterMultimedia = value && MultimediaTimerInterval > 0;

                if (enterMultimedia)
                {
                    ThreadingTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    if (MultimediaTimer == null)
                    {
                        MultimediaTimer = NativeTiming.CreateTimer(
                            MultimediaTimerInterval, MultimediaTimerInterval, MultimediaTimerCallback);
                    }

                    ActiveTimer = TimerType.Multimedia;
                }
                else
                {
                    MultimediaTimer?.Dispose();
                    MultimediaTimer = null;
                    ThreadingTimer.Change(ThreadingTimerInterval, ThreadingTimerInterval);
                    ActiveTimer = TimerType.Threading;
                }
            }
        }

        /// <summary>
        /// Gets the timer resolution interval in milliseconds.
        /// </summary>
        public int Resolution => ActiveTimer == TimerType.Multimedia
            ? MultimediaTimerInterval
            : ThreadingTimerInterval;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is running cycle to prevent reentrancy.
        /// </summary>
        private bool IsRunningCycle
        {
            get => Interlocked.CompareExchange(ref m_IsRunningCycle, 0, 0) != 0;
            set => Interlocked.Exchange(ref m_IsRunningCycle, value ? 1 : 0);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is disposing.
        /// </summary>
        private bool IsDisposing
        {
            get => Interlocked.CompareExchange(ref m_IsDisposing, 0, 0) != 0;
            set => Interlocked.Exchange(ref m_IsDisposing, value ? 1 : 0);
        }

        /// <summary>
        /// Gets or sets the currently active timer.
        /// </summary>
        private TimerType ActiveTimer
        {
            get => (TimerType)Interlocked.CompareExchange(ref m_ActiveTimer, 0, 0);
            set => Interlocked.Exchange(ref m_ActiveTimer, (int)value);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            IsRunningCycle = true;
            ActiveTimer = TimerType.None;

            if (IsDisposing) return;
            IsDisposing = true;

            MultimediaTimer?.Dispose();
            MultimediaTimer = null;

            ThreadingTimer.Dispose();
        }

        /// <summary>
        /// Implements the callback for the threading timer.
        /// </summary>
        /// <param name="state">The state.</param>
        private void ThreadingTimerCallback(object state)
        {
            if (IsRunningCycle || ActiveTimer != TimerType.Threading || IsDisposing)
                return;

            try
            {
                IsRunningCycle = true;
                UserCallback?.Invoke();
            }
            finally
            {
                IsRunningCycle = false;
            }
        }

        /// <summary>
        /// Implements the callback for the multimedia timer.
        /// </summary>
        private void MultimediaTimerCallback()
        {
            if (IsRunningCycle || ActiveTimer != TimerType.Multimedia || IsDisposing)
                return;

            try
            {
                IsRunningCycle = true;
                UserCallback?.Invoke();
            }
            finally
            {
                IsRunningCycle = false;
            }
        }
    }
}
