namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a timer for discrete event firing.
    /// Execution of callbacks is ensured non re-entrant.
    /// A single thread is used to execute callbacks in <see cref="ThreadPool"/> threads
    /// for all registered <see cref="StepTimer"/> instances. This effectively reduces
    /// the amount <see cref="Timer"/> instances when many of such objects are required.
    /// </summary>
    internal sealed class StepTimer : IDisposable
    {
        private static readonly Stopwatch Stopwatch = new Stopwatch();
        private static readonly List<StepTimer> RegisteredTimers = new List<StepTimer>();
        private static readonly ConcurrentQueue<StepTimer> PendingAddTimers = new ConcurrentQueue<StepTimer>();
        private static readonly ConcurrentQueue<StepTimer> PendingRemoveTimers = new ConcurrentQueue<StepTimer>();

        private static readonly Thread TimerThread = new Thread(ExecuteCallbacks)
        {
            IsBackground = true,
            Name = nameof(StepTimer),
            Priority = ThreadPriority.AboveNormal
        };

        private static double TickCount;

        private readonly Action UserCallback;
        private int m_IsDisposing;
        private int m_IsRunningCycle;

        /// <summary>
        /// Initializes static members of the <see cref="StepTimer"/> class.
        /// </summary>
        static StepTimer()
        {
            Resolution = Constants.DefaultTimingPeriod;
            Stopwatch.Start();
            TimerThread.Start();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StepTimer"/> class.
        /// </summary>
        /// <param name="callback">The callback.</param>
        public StepTimer(Action callback)
        {
            UserCallback = callback;
            PendingAddTimers.Enqueue(this);
        }

        /// <summary>
        /// Gets the current time interval at which callbacks are being enqueued.
        /// </summary>
        public static TimeSpan Resolution
        {
            get;
            private set;
        }

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
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            IsRunningCycle = true;
            if (IsDisposing) return;
            IsDisposing = true;
            PendingRemoveTimers.Enqueue(this);
        }

        /// <summary>
        /// Implements the execute-wait cycles of the thread.
        /// </summary>
        /// <param name="state">The state.</param>
        private static void ExecuteCallbacks(object state)
        {
            while (true)
            {
                TickCount++;
                if (TickCount >= 60)
                {
                    Resolution = TimeSpan.FromMilliseconds(Stopwatch.Elapsed.TotalMilliseconds / TickCount);
                    Stopwatch.Restart();
                    TickCount = 0;

                    // Debug.WriteLine($"Timer Resolution is now {Resolution.TotalMilliseconds}");
                }

                Parallel.ForEach(RegisteredTimers, (t) =>
                {
                    if (t.IsRunningCycle || t.IsDisposing)
                        return;

                    t.IsRunningCycle = true;

                    Task.Run(() =>
                    {
                        try
                        {
                            t.UserCallback?.Invoke();
                        }
                        finally
                        {
                            t.IsRunningCycle = false;
                        }
                    });
                });

                while (PendingAddTimers.TryDequeue(out var addTimer))
                    RegisteredTimers.Add(addTimer);

                while (PendingRemoveTimers.TryDequeue(out var remTimer))
                    RegisteredTimers.Remove(remTimer);

                Task.Delay(Constants.DefaultTimingPeriod).Wait();
            }
        }
    }
}
