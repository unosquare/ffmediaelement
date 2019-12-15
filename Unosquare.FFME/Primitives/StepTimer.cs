namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a timer for discrete intervals.
    /// It provides access to high resolution time quanta when available.
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

        static StepTimer()
        {
            Resolution = TimeSpan.FromMilliseconds(1000d / 60d);
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

                    ThreadPool.QueueUserWorkItem((s) =>
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

                Thread.Sleep(1);
            }
        }
    }
}
