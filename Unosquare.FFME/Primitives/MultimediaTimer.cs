namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents a Windows-only Multimedia timer.
    /// </summary>
    internal sealed class MultimediaTimer : IDisposable
    {
        // Hold the timer callback to prevent garbage collection.
        private readonly MultimediaTimerCallback Callback;

        // Private state variables
        private bool m_IsDisposed;
        private int m_Interval;
        private int m_Resolution;
        private uint m_TimerId;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultimediaTimer"/> class.
        /// </summary>
        /// <param name="resolution">The resolution.</param>
        /// <param name="interval">The interval.</param>
        public MultimediaTimer(int resolution, int interval)
        {
            Callback = new MultimediaTimerCallback(TimerCallbackMethod);
            Resolution = resolution;
            Interval = interval;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="MultimediaTimer"/> class.
        /// </summary>
        ~MultimediaTimer()
        {
            Dispose(false);
        }

        /// <summary>
        /// The interop delegate that gets called by the WinMM API when a tick event occurs.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="msg">The MSG.</param>
        /// <param name="userCtx">The user CTX.</param>
        /// <param name="rsv1">The RSV1.</param>
        /// <param name="rsv2">The RSV2.</param>
        private delegate void MultimediaTimerCallback(uint id, uint msg, ref uint userCtx, uint rsv1, uint rsv2);

        /// <summary>
        /// Occurs when a timer tick is executed.
        /// </summary>
        public event EventHandler Elapsed;

        /// <summary>
        /// Gets the interval in milliseconds.
        /// Must be greater than resolution.
        /// </summary>
        public int Interval
        {
            get
            {
                return m_Interval;
            }
            private set
            {
                ThrowIfDisposed();

                if (value < 0) value = 0;
                m_Interval = value;
                if (Resolution > Interval)
                    Resolution = value;
            }
        }

        /// <summary>
        /// Gets the resolution in milliseconds.
        /// Must be smaller than interval.
        /// Note minimum resolution is 0, meaning highest possible resolution.
        /// </summary>
        public int Resolution
        {
            get
            {
                return m_Resolution;
            }
            private set
            {
                ThrowIfDisposed();

                if (value < 0) value = 0;
                m_Resolution = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Timer has been started.
        /// </summary>
        public bool IsRunning => m_TimerId != 0;

        /// <summary>
        /// Starts the timer.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Occurs when the timer is already running or has been disposed.
        /// </exception>
        public void Start()
        {
            ThrowIfDisposed();

            if (IsRunning)
                throw new InvalidOperationException($"{nameof(MultimediaTimer)} is already running");

            // Event type = 0, one off event
            // Event type = 1, periodic event
            uint userContext = 0;
            m_TimerId = NativeMethods.TimeSetEvent((uint)Interval, (uint)Resolution, Callback, ref userContext, 1);
            if (m_TimerId == 0)
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Win32 Error Code: {error}");
            }
        }

        /// <summary>
        /// Stops the timer.
        /// </summary>
        /// <exception cref="InvalidOperationException">Timer has not been started.</exception>
        public void Stop()
        {
            ThrowIfDisposed();

            if (!IsRunning)
                throw new InvalidOperationException("Timer has not been started");

            StopInternal();
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
        /// Internal call for stopping the timer.
        /// </summary>
        private void StopInternal()
        {
            NativeMethods.TimeKillEvent(m_TimerId);
            m_TimerId = 0;
        }

        /// <summary>
        /// The interop method matching the delegate.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="message">The MSG.</param>
        /// <param name="userContext">The user CTX.</param>
        /// <param name="reserved1">The RSV1.</param>
        /// <param name="reserved2">The RSV2.</param>
        private void TimerCallbackMethod(uint id, uint message, ref uint userContext, uint reserved1, uint reserved2)
        {
            Elapsed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Checks if the timer has been disposed. If it has, then it throws an exception.
        /// </summary>
        /// <exception cref="ObjectDisposedException">MultimediaTimer has been disposed.</exception>
        private void ThrowIfDisposed()
        {
            if (m_IsDisposed)
                throw new ObjectDisposedException(nameof(MultimediaTimer));
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (m_IsDisposed)
                return;

            m_IsDisposed = true;
            if (IsRunning)
            {
                StopInternal();
            }

            if (disposing)
            {
                Elapsed = null;
            }
        }

        /// <summary>
        /// Interop API for WinMM.
        /// </summary>
        private static class NativeMethods
        {
            private const string WinMM = "winmm.dll";

            [DllImport(WinMM, SetLastError = true, EntryPoint = "timeSetEvent")]
            internal static extern uint TimeSetEvent(uint millisecondsDelay, uint millisecondsResolution, MultimediaTimerCallback callback, ref uint userContext, uint eventType);

            [DllImport(WinMM, SetLastError = true, EntryPoint = "timeKillEvent")]
            internal static extern void TimeKillEvent(uint timerId);
        }
    }
}
