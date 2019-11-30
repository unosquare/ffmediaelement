namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A base class for implementing interval workers.
    /// </summary>
    internal abstract class IntervalWorkerBase : IWorker
    {
        private const int WantedTimingResolution = 2;
        private readonly object SyncLock = new object();
        private readonly Thread Thread;
        private readonly RealTimeClock CycleClock = new RealTimeClock();
        private readonly ManualResetEventSlim WantedStateCompleted = new ManualResetEventSlim(true);

        private CancellationTokenSource TokenSource = new CancellationTokenSource();

        private long m_Period;
        private int m_IsDisposed;
        private int m_IsDisposing;
        private int m_WorkerState = (int)WorkerState.Created;
        private int m_WantedWorkerState = (int)WorkerState.Running;

        protected IntervalWorkerBase(string name, TimeSpan period, IntervalWorkerMode mode, ThreadPriority priority)
        {
            // TODO: Still need to document this class
            Name = name;
            Period = period;
            Thread = new Thread(RunThread)
            {
                IsBackground = true,
                Name = $"{name}Thread",
                Priority = priority,
            };

            // Enable shorter scheduling times to save CPU
            if (TimingConfiguration.IsHighResolution)
            {
                var appliedResolution = TimingConfiguration.MinimumPeriod > WantedTimingResolution
                    ? TimingConfiguration.MinimumPeriod
                    : WantedTimingResolution;

                if (TimingConfiguration.ChangePeriod(appliedResolution))
                {
                    Resolution = appliedResolution;
                }
            }

            Mode = mode;
        }

        public string Name { get; }

        public int Resolution { get; } = 15;

        public IntervalWorkerMode Mode { get; protected set; }

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

        /// <summary>
        /// Gets the remaining cycle time.
        /// </summary>
        protected TimeSpan RemainingCycleTime => TimeSpan.FromTicks(Period.Ticks - CycleClock.Position.Ticks);

        protected TimeSpan LastCycleElapsed { get; private set; }

        protected TimeSpan NextCorrectionDelay { get; set; }

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
                CycleClock.Reset();
                WantedStateCompleted.Set();
                WantedStateCompleted.Dispose();
                TokenSource.Dispose();
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
        protected void Interrupt() => TokenSource.Cancel();

        /// <summary>
        /// Implements an efficient delay.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void Delay()
        {
            while (RemainingCycleTime.Ticks > 0)
            {
                if (WantedWorkerState != WorkerState || TokenSource.IsCancellationRequested)
                    break;

                var remainingMs = (int)RemainingCycleTime.TotalMilliseconds;
                if (Mode == IntervalWorkerMode.HighPrecision)
                {
                    if (remainingMs >= Resolution * 1.5)
                        Thread.Sleep(Resolution);
                }
                else
                {
                    if (remainingMs > 0)
                        Thread.Sleep(Math.Max(15, Resolution));
                }
            }

            LastCycleElapsed = CycleClock.Position;
            CycleClock.Restart(NextCorrectionDelay.Negate());
            NextCorrectionDelay = TimeSpan.Zero;

            /*
            CycleClock.Restart( // Mode == IntervalWorkerMode.HighPrecision &&
                RemainingCycleTime.Ticks < 0 &&
                RemainingCycleTime.TotalMilliseconds >= -Period.TotalMilliseconds
                ? RemainingCycleTime.Negate()
                : TimeSpan.Zero);
            */
        }

        /// <summary>
        /// Perofrms worker operations in a loop.
        /// </summary>
        /// <param name="state">The state.</param>
        private void RunThread(object state)
        {
            // Control variable setup
            CycleClock.Restart();

            while (WorkerState != WorkerState.Stopped)
            {
                lock (SyncLock)
                {
                    if (WantedWorkerState == WorkerState.Stopped)
                        break;

                    WorkerState = WantedWorkerState;
                    WantedStateCompleted.Set();
                }

                // Recreate the token source -- applies to cycle logic and delay
                if (TokenSource.IsCancellationRequested)
                {
                    TokenSource.Dispose();
                    TokenSource = new CancellationTokenSource();
                }

                if (WorkerState == WorkerState.Running)
                {
                    try
                    {
                        ExecuteCycleLogic(TokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        OnCycleException(ex);
                    }
                }

                Delay();
            }

            WorkerState = WorkerState.Stopped;
            WantedStateCompleted.Set();
        }
    }
}
