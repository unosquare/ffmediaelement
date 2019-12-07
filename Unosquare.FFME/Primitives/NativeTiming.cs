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
    internal static class NativeTiming
    {
        private static readonly object SyncLock = new object();
        private static int? CurrentResolution;

        /// <summary>
        /// Initializes static members of the <see cref="NativeTiming"/> class.
        /// </summary>
        static NativeTiming()
        {
            try
            {
                var caps = default(PeriodCapabilities);
                var result = NativeMethods.GetDeviceCapabilities(ref caps, Marshal.SizeOf<PeriodCapabilities>());
                if (result == 0)
                {
                    MinResolutionPeriod = caps.PeriodMin;
                    MaxResolutionPeriod = caps.PeriodMax;
                    IsAvailable = true;
                    return;
                }
            }
            catch
            {
                // ignore
            }

            MinResolutionPeriod = 16;
            MaxResolutionPeriod = 16;
            IsAvailable = false;
        }

        /// <summary>
        /// The interop delegate for multimedia timers.
        /// </summary>
        /// <param name="timerId">The timer identifier.</param>
        /// <param name="message">The message.</param>
        /// <param name="userContext">The user context.</param>
        /// <param name="reserved1">The reserved1.</param>
        /// <param name="reserved2">The reserved2.</param>
        private delegate void NativeTimerCallback(uint timerId, uint message, UIntPtr userContext, UIntPtr reserved1, UIntPtr reserved2);

        /// <summary>
        /// Gets a value indicating whether the system supports changing timing configuration.
        /// </summary>
        public static bool IsAvailable { get; }

        /// <summary>
        /// Gets the minimum period resolution in milliseconds.
        /// </summary>
        public static int MinResolutionPeriod { get; }

        /// <summary>
        /// Gets the maximum period resolution in milliseconds.
        /// </summary>
        public static int MaxResolutionPeriod { get; }

        /// <summary>
        /// Gets the currently configured resolution in milliseconds.
        /// </summary>
        public static int? Resolution
        {
            get
            {
                lock (SyncLock)
                    return CurrentResolution;
            }
        }

        /// <summary>
        /// Changes configured the resolution in milliseconds.
        /// Pass 0 for best resolution possible.
        /// </summary>
        /// <param name="newPeriod">The new period.</param>
        /// <returns>If the operation was successful.</returns>
        public static bool ChangeResolution(int newPeriod)
        {
            lock (SyncLock)
            {
                if (!IsAvailable)
                    return false;

                var appliedPeriod = newPeriod.ClampResolution();

                if (CurrentResolution.HasValue && CurrentResolution.Value == appliedPeriod)
                    return true;

                ResetResolution();
                var success = NativeMethods.BeginUsingPeriod(appliedPeriod) == 0;
                if (success)
                    CurrentResolution = appliedPeriod;

                return success;
            }
        }

        /// <summary>
        /// Resets the period to its default.
        /// </summary>
        /// <returns>True if the operation was successful.</returns>
        public static bool ResetResolution()
        {
            lock (SyncLock)
            {
                if (!CurrentResolution.HasValue)
                    return false;

                var success = NativeMethods.EndUsingPeriod(CurrentResolution.Value) == 0;
                if (success)
                    CurrentResolution = null;

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
        public static IDisposable ScheduleAction(int delay, int resolution, Action callback) =>
            delay <= 0 ? throw new ArgumentOutOfRangeException($"{nameof(delay)} must be greater than 0.") :
            IsAvailable
            ? new WindowsTimerHandle(delay, resolution.ClampResolution(), false, callback)
            : throw new NotSupportedException($"{nameof(NativeTiming)} is not supported in this OS.");

        /// <summary>
        /// Creates a multimedia timer.
        /// </summary>
        /// <param name="period">The interval in milliceonds.</param>
        /// <param name="resolution">The resolution in milliseconds to measure a sub-interval in the period.</param>
        /// <param name="callback">The action to be called. Occurs in a thread pool thread.</param>
        /// <returns>>A disposable handle to the timer.</returns>
        public static IDisposable CreateTimer(int period, int resolution, Action callback) =>
            period <= 0 ? throw new ArgumentOutOfRangeException($"{nameof(period)} must be greater than 0.") :
            IsAvailable
            ? new WindowsTimerHandle(period, resolution.ClampResolution(), true, callback)
            : throw new NotSupportedException($"{nameof(NativeTiming)} is not supported in this OS.");

        /// <summary>
        /// Clamps the resolution.
        /// </summary>
        /// <param name="resolution">The resolution.</param>
        /// <returns>The clamped value between minimum and maximum period.</returns>
        private static int ClampResolution(this int resolution) => resolution < MinResolutionPeriod
            ? MinResolutionPeriod
            : resolution > MaxResolutionPeriod ? MaxResolutionPeriod : resolution;

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
            public static extern uint BeginTimer(uint delay, uint resolution, NativeTimerCallback timerCallback, UIntPtr dwUser, uint eventType);

            [DllImport(WinMM, EntryPoint = "timeKillEvent")]
            public static extern uint EndTimer(uint timerId);
        }

        /// <summary>
        /// Represents a Windows-specific multimedia timer handle.
        /// </summary>
        private sealed class WindowsTimerHandle : IDisposable
        {
            private readonly Action UserCallback;
            private readonly NativeTimerCallback InternalCallback;
            private readonly TimerEventType EventType;

            private long m_IsDisposing;
            private long m_IsDisposed;

            /// <summary>
            /// Initializes a new instance of the <see cref="WindowsTimerHandle"/> class.
            /// </summary>
            /// <param name="delay">The delay.</param>
            /// <param name="resolution">The resolution.</param>
            /// <param name="isPeriodic">if set to <c>true</c> [is periodic].</param>
            /// <param name="callback">The callback.</param>
            public WindowsTimerHandle(int delay, int resolution, bool isPeriodic, Action callback)
            {
                UserCallback = callback;
                EventType = isPeriodic ? TimerEventType.Periodic : TimerEventType.OneShot;
                InternalCallback = InternalCallbackImpl;
                TimerId = NativeMethods.BeginTimer((uint)delay, (uint)resolution, InternalCallback, UIntPtr.Zero, (uint)EventType);
            }

            /// <summary>
            /// Finalizes an instance of the <see cref="WindowsTimerHandle"/> class.
            /// </summary>
            ~WindowsTimerHandle() => Dispose(false);

            /// <summary>
            /// Defines the type of timer handle.
            /// </summary>
            [Flags]
            private enum TimerEventType : uint
            {
                /// <summary>
                /// The one shot type.
                /// </summary>
                OneShot = 0,

                /// <summary>
                /// The periodic type.
                /// </summary>
                Periodic = 1,
            }

            /// <summary>
            /// Gets the timer identifier.
            /// </summary>
            public uint TimerId { get; }

            /// <summary>
            /// Gets or sets a value indicating whether this instance is disposing.
            /// </summary>
            private bool IsDisposing
            {
                get => Interlocked.Read(ref m_IsDisposing) != 0;
                set => Interlocked.Exchange(ref m_IsDisposing, value ? 1 : 0);
            }

            /// <summary>
            /// Gets or sets a value indicating whether this instance is disposed.
            /// </summary>
            private bool IsDisposed
            {
                get => Interlocked.Read(ref m_IsDisposed) != 0;
                set => Interlocked.Exchange(ref m_IsDisposed, value ? 1 : 0);
            }

            /// <summary>
            /// Releases unmanaged and - optionally - managed resources.
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// Implements the timer callback.
            /// </summary>
            /// <param name="timerId">The timer identifier.</param>
            /// <param name="message">The message.</param>
            /// <param name="userContext">The user context.</param>
            /// <param name="reserved1">The reserved1.</param>
            /// <param name="reserved2">The reserved2.</param>
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

            /// <summary>
            /// Releases unmanaged and - optionally - managed resources.
            /// </summary>
            /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
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
