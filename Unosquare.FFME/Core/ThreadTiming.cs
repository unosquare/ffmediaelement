namespace Unosquare.FFME.Core
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides thread timing function alternatives to the
    /// blocking Thread.Sleep or the overhead-incurring await Task.Delay 
    /// </summary>
    internal sealed class ThreadTiming
    {
        #region Private Members

        private const int IntervalMilliseconds = 8;

        private readonly System.Timers.Timer Timer = null;
        private readonly ManualResetEvent TimerDone = new ManualResetEvent(false);
        private readonly Stopwatch Stopwatch = new Stopwatch();

        static private readonly object SyncLock = new object();

        private static Lazy<ThreadTiming> m_Instance = new Lazy<ThreadTiming>(() =>
        {
            lock (SyncLock)
                return new ThreadTiming();
        }, true);

        #endregion

        #region Constructor and Instance Accessor

        /// <summary>
        /// Prevents a default instance of the <see cref="ThreadTiming"/> class from being created.
        /// </summary>
        private ThreadTiming()
        {
            Timer = new System.Timers.Timer(IntervalMilliseconds);

            Timer.Elapsed += (s, e) =>
            {
                TimerDone.Set();
                TimerDone.Reset();
            };

            Timer.Start();
            Stopwatch.Start();
        }

        /// <summary>
        /// Gets the singleton, lazy-loaded instance.
        /// </summary>
        private static ThreadTiming Instance { get { return m_Instance.Value; } }

        #endregion

        #region Public API

        /// <summary>
        /// Suspends the thread for at most the specified timeout.
        /// </summary>
        public static void Suspend(int timeoutMilliseconds)
        {
            Instance.TimerDone.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// Suspends the thread for at most 1 timer cycle.
        /// </summary>
        public static void SuspendOne()
        {
            Instance.TimerDone.WaitOne();
        }

        /// <summary>
        /// Alternative thread sleep method by suspending the thread by at least
        /// the sepcifed timeout milliseconds.
        /// </summary>
        /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
        public static void Sleep(int timeoutMilliseconds)
        {
            var startMillis = Instance.Stopwatch.ElapsedMilliseconds;
            var elapsedMillis = default(long);
            do
            {
                SuspendOne();
                elapsedMillis = Instance.Stopwatch.ElapsedMilliseconds - startMillis;
            } while (elapsedMillis < timeoutMilliseconds);
        }

        /// <summary>
        /// Calls the classic Thread.Sleep method directly.
        /// </summary>
        /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
        public static void SleepBlock(int timeoutMilliseconds)
        {
            Thread.Sleep(timeoutMilliseconds);
        }

        /// <summary>
        /// Calls the classic Task.Delay method directly.
        /// </summary>
        /// <param name="timeoutMilliseconds">The timeout milliseconds.</param>
        /// <returns></returns>
        public static async Task SuspendAsync(int timeoutMilliseconds)
        {
            await Task.Delay(timeoutMilliseconds);
        }

        #endregion
    }
}
