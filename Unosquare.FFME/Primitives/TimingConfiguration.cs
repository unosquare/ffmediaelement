namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    /// Provides access to advanced timing configuration.
    /// Under Windows, this class uses the Windows Multimedia API
    /// to configure timing.
    /// </summary>
    internal static class TimingConfiguration
    {
        private static readonly object SyncLock = new object();
        private static int? CurrentPeriod;

        /// <summary>
        /// Initializes static members of the <see cref="TimingConfiguration"/> class.
        /// </summary>
        static TimingConfiguration()
        {
            try
            {
                var caps = default(PeriodCapabilities);
                var result = NativeMethods.GetDeviceCapabilities(ref caps, Marshal.SizeOf<PeriodCapabilities>());
                MinimumPeriod = caps.PeriodMin;
                MaximumPeriod = caps.PeriodMax;
                IsAvailable = true;
            }
            catch
            {
                MinimumPeriod = 16;
                MaximumPeriod = 16;
                IsAvailable = false;
            }
        }

        /// <summary>
        /// The interop delegate for multimedia timers.
        /// </summary>
        /// <param name="timerId">The timer identifier.</param>
        /// <param name="message">The message.</param>
        /// <param name="userContext">The user context.</param>
        /// <param name="reserved1">The reserved1.</param>
        /// <param name="reserved2">The reserved2.</param>
        private delegate void MultimediaTimerCallback(uint timerId, uint message, UIntPtr userContext, UIntPtr reserved1, UIntPtr reserved2);

        /// <summary>
        /// Gets a value indicating whether the system supports changing timing configuration.
        /// </summary>
        public static bool IsAvailable { get; }

        /// <summary>
        /// Gets the minimum period in milliseconds.
        /// </summary>
        public static int MinimumPeriod { get; }

        /// <summary>
        /// Gets the maximum period in milliseconds.
        /// </summary>
        public static int MaximumPeriod { get; }

        /// <summary>
        /// Gets the currently configured period in milliseconds.
        /// </summary>
        public static int? Period
        {
            get
            {
                lock (SyncLock)
                    return CurrentPeriod;
            }
        }

        /// <summary>
        /// Changes configured the period in milliseconds.
        /// </summary>
        /// <param name="newPeriod">The new period.</param>
        /// <returns>If the operation was successful.</returns>
        public static bool ChangePeriod(int newPeriod)
        {
            lock (SyncLock)
            {
                if (!IsAvailable)
                    return false;

                if (CurrentPeriod.HasValue && CurrentPeriod.Value == newPeriod)
                    return true;

                ResetPeriod();
                var success = NativeMethods.BeginUsingPeriod(newPeriod) == 0;
                if (success)
                    CurrentPeriod = newPeriod;

                return success;
            }
        }

        /// <summary>
        /// Resets the period to its default.
        /// </summary>
        /// <returns>True if the operation was successful.</returns>
        public static bool ResetPeriod()
        {
            lock (SyncLock)
            {
                if (!CurrentPeriod.HasValue)
                    return false;

                var success = NativeMethods.EndUsingPeriod(CurrentPeriod.Value) == 0;
                if (success)
                    CurrentPeriod = null;

                return success;
            }
        }

        /// <summary>
        /// Schedules an action to occur in the future.
        /// </summary>
        /// <param name="delay">The delay in milliseconds before the action is called.</param>
        /// <param name="resolution">The resolution in milliseconds to measure a sub-interval in the delay.</param>
        /// <param name="callback">The action to be called. Occurs in a thread pool thread.</param>
        /// <returns>A disposable handle to the scheduled event.</returns>
        public static IDisposable ScheduleAction(int delay, int resolution, Action callback)
            => new TimerEvent(delay, resolution, false, callback);

        /// <summary>
        /// Creates a multimedia timer.
        /// </summary>
        /// <param name="period">The interval in milliceonds.</param>
        /// <param name="resolution">The resolution in milliseconds to measure a sub-interval in the period.</param>
        /// <param name="callback">The action to be called. Occurs in a thread pool thread.</param>
        /// <returns>>A disposable handle to the timer.</returns>
        public static IDisposable CreateTimer(int period, int resolution, Action callback)
            => new TimerEvent(period, resolution, true, callback);

        /// <summary>
        /// A struct to exctract timing capabilities from the interop API call.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct PeriodCapabilities
        {
            public int PeriodMin;

            public int PeriodMax;
        }

        /// <summary>
        /// Provides the native methods for timing configuration.
        /// </summary>
        private static class NativeMethods
        {
            private const string WinMM = "winmm.dll";

            [DllImport(WinMM, EntryPoint = "timeGetDevCaps")]
            public static extern int GetDeviceCapabilities(ref PeriodCapabilities ptc, int cbtc);

            [DllImport(WinMM, EntryPoint = "timeBeginPeriod")]
            public static extern int BeginUsingPeriod(int periodMillis);

            [DllImport(WinMM, EntryPoint = "timeEndPeriod")]
            public static extern int EndUsingPeriod(int periodMillis);

            [DllImport(WinMM, EntryPoint = "timeSetEvent")]
            public static extern uint BeginTimer(uint delay, uint resolution, MultimediaTimerCallback timerCallback, UIntPtr dwUser, uint eventType);

            [DllImport(WinMM, EntryPoint = "timeKillEvent")]
            public static extern uint EndTimer(uint timerId);
        }

        private sealed class TimerEvent : IDisposable
        {
            private readonly Action UserCallback;
            private readonly MultimediaTimerCallback InternalCallback;
            private readonly TimerEventType EventType;

            private long m_IsDisposing;
            private long m_IsDisposed;

            public TimerEvent(int delay, int resolution, bool isPeriodic, Action callback)
            {
                UserCallback = callback;
                EventType = isPeriodic ? TimerEventType.Periodic : TimerEventType.OneShot;
                InternalCallback = new MultimediaTimerCallback(InternalCallbackImpl);
                TimerId = NativeMethods.BeginTimer((uint)delay, (uint)resolution, InternalCallback, UIntPtr.Zero, (uint)EventType);
            }

            ~TimerEvent() => Dispose(false);

            [Flags]
            private enum TimerEventType : uint
            {
                OneShot = 0,
                Periodic = 1,
            }

            public uint TimerId { get; }

            private bool IsDisposing
            {
                get => Interlocked.Read(ref m_IsDisposing) != 0;
                set => Interlocked.Exchange(ref m_IsDisposing, value ? 1 : 0);
            }

            private bool IsDisposed
            {
                get => Interlocked.Read(ref m_IsDisposed) != 0;
                set => Interlocked.Exchange(ref m_IsDisposed, value ? 1 : 0);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void InternalCallbackImpl(uint timerId, uint message, UIntPtr userContext, UIntPtr reserved1, UIntPtr reserved2)
            {
                if (IsDisposing) return;
                try
                {
                    UserCallback?.Invoke();
                }
                finally
                {
                    if (EventType == TimerEventType.OneShot)
                        Dispose();
                }
            }

            private void Dispose(bool alsoManaged)
            {
                IsDisposing = true;
                if (IsDisposed) return;
                IsDisposed = true;

                if (alsoManaged)
                {
                    // Note: Noting managed to dispose here
                }

                // Free unmanaged resources
                var resultCode = NativeMethods.EndTimer(TimerId);
            }
        }
    }
}
