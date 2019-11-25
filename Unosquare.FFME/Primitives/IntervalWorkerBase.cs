namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class IntervalWorkerBase : IWorker
    {
        private readonly object SyncLock = new object();
        private readonly Thread Thread;
        private readonly Stopwatch Chrono = new Stopwatch();
        private readonly ManualResetEventSlim WantedStateCompleted = new ManualResetEventSlim(true);
        private readonly MultimediaTimer Timer;
        private readonly ManualResetEventSlim TimerTicked = new ManualResetEventSlim(true);
        private CancellationTokenSource TokenSource = new CancellationTokenSource();

        private long m_Period;
        private int m_IsDisposed;
        private int m_IsDisposing;
        private int m_WorkerState = (int)WorkerState.Created;
        private int m_WantedWorkerState = (int)WorkerState.Running;
        private int SleepInterval;

        protected IntervalWorkerBase(string name, TimeSpan period, ThreadPriority priority, IntervalWorkerMode desiredMode)
        {
            Name = name;
            Period = period;
            Thread = new Thread(RunThread)
            {
                IsBackground = true,
                Name = $"{name}Thread",
                Priority = priority,
            };

            switch (desiredMode)
            {
                case IntervalWorkerMode.DefaultSleepLoop:
                    {
                        SleepInterval = 15;
                        Mode = desiredMode;
                        break;
                    }

                case IntervalWorkerMode.ShortSleepLoop:
                    {
                        SleepInterval = 1;
                        Mode = desiredMode;
                        break;
                    }

                case IntervalWorkerMode.TightLoop:
                    {
                        SleepInterval = 0;
                        Mode = desiredMode;
                        break;
                    }

                case IntervalWorkerMode.Multimedia:
                    {
                        try
                        {
                            Timer = new MultimediaTimer((int)(period.TotalMilliseconds / 2d), (int)period.TotalMilliseconds);
                            Timer.Elapsed += (s, e) =>
                            {
                                TimerTicked.Set();
                            };

                            Mode = desiredMode;
                            SleepInterval = 1;
                        }
                        catch
                        {
                            Mode = IntervalWorkerMode.ShortSleepLoop;
                            SleepInterval = 1;
                        }

                        break;
                    }

                default:
                    {
                        throw new ArgumentException($"{nameof(desiredMode)} is invalid.");
                    }
            }
        }

        public string Name { get; }

        public IntervalWorkerMode Mode { get; }

        /// <inheritdoc />
        public TimeSpan Period
        {
            get => TimeSpan.FromTicks(Interlocked.Read(ref m_Period));
            set => Interlocked.Exchange(ref m_Period, value.Ticks < 0 ? 0 : value.Ticks);
        }

        /// <inheritdoc />
        public WorkerState WorkerState
        {
            get => (WorkerState)Interlocked.CompareExchange(ref m_WorkerState, 0, 0);
            protected set => Interlocked.Exchange(ref m_WorkerState, (int)value);
        }

        /// <inheritdoc />
        public bool IsDisposed
        {
            get => Interlocked.CompareExchange(ref m_IsDisposed, 0, 0) != 0;
            protected set => Interlocked.Exchange(ref m_IsDisposed, value ? 1 : 0);
        }

        /// <inheritdoc />
        public bool IsDisposing
        {
            get => Interlocked.CompareExchange(ref m_IsDisposing, 0, 0) != 0;
            protected set => Interlocked.Exchange(ref m_IsDisposing, value ? 1 : 0);
        }

        /// <summary>
        /// Gets or sets the desired state of the worker.
        /// </summary>
        protected WorkerState WantedWorkerState
        {
            get => (WorkerState)Interlocked.CompareExchange(ref m_WantedWorkerState, 0, 0);
            set => Interlocked.Exchange(ref m_WantedWorkerState, (int)value);
        }

        /// <inheritdoc />
        public Task<WorkerState> StartAsync()
        {
            var awaitTask = false;
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return Task.FromResult(WorkerState);

                Interrupt();

                if (WorkerState == WorkerState.Created)
                {
                    WantedWorkerState = WorkerState.Running;
                    WorkerState = WorkerState.Running;
                    Timer?.Start();
                    Thread.Start();
                }
                else if (WorkerState == WorkerState.Paused)
                {
                    awaitTask = true;
                    WantedStateCompleted.Reset();
                    WantedWorkerState = WorkerState.Running;
                }

                if (!awaitTask)
                    return Task.FromResult(WorkerState);
            }

            return Task.Run(() =>
            {
                WantedStateCompleted.Wait();
                return WorkerState;
            });
        }

        /// <inheritdoc />
        public Task<WorkerState> PauseAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return Task.FromResult(WorkerState);

                Interrupt();

                if (WorkerState != WorkerState.Running)
                    return Task.FromResult(WorkerState);

                WantedStateCompleted.Reset();
                WantedWorkerState = WorkerState.Paused;
            }

            return Task.Run(() =>
            {
                WantedStateCompleted.Wait();
                return WorkerState;
            });
        }

        /// <inheritdoc />
        public Task<WorkerState> ResumeAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return Task.FromResult(WorkerState);

                Interrupt();

                if (WorkerState != WorkerState.Paused)
                    return Task.FromResult(WorkerState);

                WantedStateCompleted.Reset();
                WantedWorkerState = WorkerState.Running;
            }

            return Task.Run(() =>
            {
                WantedStateCompleted.Wait();
                return WorkerState;
            });
        }

        /// <inheritdoc />
        public Task<WorkerState> StopAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return Task.FromResult(WorkerState);

                Interrupt();

                if (WorkerState != WorkerState.Running && WorkerState != WorkerState.Paused)
                    return Task.FromResult(WorkerState);

                WantedStateCompleted.Reset();
                WantedWorkerState = WorkerState.Stopped;
            }

            return Task.Run(() =>
            {
                WantedStateCompleted.Wait();
                return WorkerState;
            });
        }

        /// <inheritdoc />
        public virtual void Dispose() => Dispose(true);

        /// <summary>
        /// Releases unmanaged and optionally managed resources.
        /// </summary>
        /// <param name="alsoManaged">Determines if managed resources hsould also be released.</param>
        protected virtual void Dispose(bool alsoManaged)
        {
            StopAsync().Wait();

            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return;

                IsDisposing = true;
                OnDisposing();
                Chrono.Stop();
                WantedStateCompleted.Set();
                WantedStateCompleted.Dispose();
                TokenSource.Dispose();
                if (Timer != null)
                    Timer.Dispose();

                TimerTicked.Dispose();
                IsDisposed = true;
                IsDisposing = false;
            }
        }

        /// <summary>
        /// Handles the cycle logic exceptions.
        /// </summary>
        /// <param name="ex">The exception that was thrown.</param>
        protected abstract void OnCycleException(Exception ex);

        /// <summary>
        /// Represents the user defined logic to be executed on a single worker cycle.
        /// Check the cancellation token continuously if you need responsive interrupts.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        protected abstract void ExecuteCycleLogic(CancellationToken ct);

        /// <summary>
        /// This method is called automatically when <see cref="Dispose()"/> is called.
        /// Makes sure you release all resources within this call.
        /// </summary>
        protected abstract void OnDisposing();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Interrupt()
        {
            TokenSource.Cancel();
            TimerTicked.Set();
        }

        /// <summary>
        /// Perofrms worker operations in a loop.
        /// </summary>
        /// <param name="state">The state.</param>
        private void RunThread(object state)
        {
            Chrono.Restart();

            while (WorkerState != WorkerState.Stopped)
            {
                TimerTicked.Reset();

                lock (SyncLock)
                {
                    if (WantedWorkerState == WorkerState.Stopped)
                    {
                        Timer?.Stop();
                        break;
                    }

                    WorkerState = WantedWorkerState;
                    WantedStateCompleted.Set();
                }

                if (WorkerState == WorkerState.Running)
                {
                    if (TokenSource.IsCancellationRequested)
                        TokenSource = new CancellationTokenSource();

                    try
                    {
                        ExecuteCycleLogic(TokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        OnCycleException(ex);
                    }
                }

                if (Timer != null && Timer.IsRunning && WantedWorkerState == WorkerState)
                {
                    var remainder = (int)(Period.TotalMilliseconds - Chrono.ElapsedMilliseconds);
                    if (remainder > 0)
                    {
                        try
                        {
                            TimerTicked.Wait(remainder, TokenSource.Token);
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
                else
                {
                    while (Chrono.ElapsedMilliseconds < Period.TotalMilliseconds)
                    {
                        if (WantedWorkerState != WorkerState || TokenSource.IsCancellationRequested)
                            break;

                        Thread.Sleep(SleepInterval);
                        break;
                    }
                }

                Chrono.Restart();
            }

            WorkerState = WorkerState.Stopped;
            WantedStateCompleted.Set();
        }
    }
}
