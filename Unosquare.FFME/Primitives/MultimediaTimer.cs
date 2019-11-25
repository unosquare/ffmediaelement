namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Runtime.InteropServices;

    internal sealed class MultimediaTimer : IDisposable
    {
        // Hold the timer callback to prevent garbage collection.
        private readonly MultimediaTimerCallback Callback;

        private bool m_IsDisposed;
        private int m_Interval;
        private int m_Resolution;
        private uint m_TimerId;

        public MultimediaTimer(int resolution, int interval)
        {
            Callback = new MultimediaTimerCallback(TimerCallbackMethod);
            Resolution = resolution;
            Interval = interval;
        }

        ~MultimediaTimer()
        {
            Dispose(false);
        }

        private delegate void MultimediaTimerCallback(uint id, uint msg, ref uint userCtx, uint rsv1, uint rsv2);
        public event EventHandler Elapsed;

        public int Interval
        {
            get
            {
                return m_Interval;
            }
            set
            {
                CheckDisposed();

                if (value < 0) value = 0;
                m_Interval = value;
                if (Resolution > Interval)
                    Resolution = value;
            }
        }

        // Note minimum resolution is 0, meaning highest possible resolution.
        public int Resolution
        {
            get
            {
                return m_Resolution;
            }
            set
            {
                CheckDisposed();

                if (value < 0) value = 0;
                m_Resolution = value;
            }
        }

        public bool IsRunning => m_TimerId != 0;

        public void Start()
        {
            CheckDisposed();

            if (IsRunning)
                throw new InvalidOperationException("Timer is already running");

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

        public void Stop()
        {
            CheckDisposed();

            if (!IsRunning)
                throw new InvalidOperationException("Timer has not been started");

            StopInternal();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void StopInternal()
        {
            NativeMethods.TimeKillEvent(m_TimerId);
            m_TimerId = 0;
        }

        private void TimerCallbackMethod(uint id, uint msg, ref uint userCtx, uint rsv1, uint rsv2)
        {
            Elapsed?.Invoke(this, EventArgs.Empty);
        }

        private void CheckDisposed()
        {
            if (m_IsDisposed)
                throw new ObjectDisposedException(nameof(MultimediaTimer));
        }

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

        private static class NativeMethods
        {
            private const string WinMM = "winmm.dll";

            [DllImport(WinMM, SetLastError = true, EntryPoint = "timeSetEvent")]
            internal static extern uint TimeSetEvent(uint msDelay, uint msResolution, MultimediaTimerCallback callback, ref uint userCtx, uint eventType);

            [DllImport(WinMM, SetLastError = true, EntryPoint = "timeKillEvent")]
            internal static extern void TimeKillEvent(uint uTimerId);
        }
    }
}
