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
        private readonly object SyncLock = new object();
        private readonly IntervalTimer QuantumTimer;
        private readonly RealTimeClock CycleClock = new RealTimeClock();
        private readonly ManualResetEventSlim WantedStateCompleted = new ManualResetEventSlim(true);

        private long m_Period;
        private int m_IsDisposed;
        private int m_IsDisposing;
        private int m_WorkerState = (int)WorkerState.Created;
        private int m_WantedWorkerState = (int)WorkerState.Running;
        private CancellationTokenSource TokenSource = new CancellationTokenSource();

        /// <summary>
        /// Initializes a new instance of the <see cref="IntervalWorkerBase"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="period">The period.</param>
        /// <param name="mode">The mode.</param>
        protected IntervalWorkerBase(string name, TimeSpan period, IntervalWorkerMode mode)
        {
            Name = name;
            Period = period;
            QuantumTimer = new IntervalTimer(mode == IntervalWorkerMode.HighPrecision, OnQuantumTicked);
        }

        /// <summary>
        /// Gets the name of the worker.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets the worker mode.
        /// This is a per-cycle setting.
        /// </summary>
        public IntervalWorkerMode Mode
        {
            get => QuantumTimer.IsMultimedia ? IntervalWorkerMode.HighPrecision : IntervalWorkerMode.SystemDefault;
            set => QuantumTimer.IsMultimedia = value == IntervalWorkerMode.HighPrecision;
        }

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

        /// <summary>
        /// Gets the elapsed time of the last cycle.
        /// </summary>
        protected TimeSpan LastCycleElapsed { get; private set; }

        /// <summary>
        /// Gets or sets the delay to add (or subtract if negative) to the current cycle.
        /// This is a per-cycle setting.
        /// </summary>
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
                    CycleClock.Restart();
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
            QuantumTimer.Dispose();

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

        /// <summary>
        /// Interrupts a cycle or a wait operation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Interrupt() => TokenSource.Cancel();

        private void OnQuantumTicked()
        {
            if (WorkerState == WorkerState.Created || WorkerState == WorkerState.Stopped)
                return;

            while (RemainingCycleTime.Ticks > 0)
            {
                if (WantedWorkerState != WorkerState || TokenSource.IsCancellationRequested)
                    break;

                if (RemainingCycleTime.TotalMilliseconds > QuantumTimer.Resolution)
                    return;

                if (!QuantumTimer.IsMultimedia && !Thread.Yield())
                    Thread.Sleep(1);
            }

            LastCycleElapsed = TimeSpan.FromTicks(CycleClock.Position.Ticks + NextCorrectionDelay.Ticks);
            CycleClock.Restart(NextCorrectionDelay.Negate());
            NextCorrectionDelay = TimeSpan.Zero;

            lock (SyncLock)
            {
                WorkerState = WantedWorkerState;
                WantedStateCompleted.Set();

                if (WorkerState == WorkerState.Stopped)
                    return;
            }

            // Recreate the token source -- applies to cycle logic and delay
            var ts = TokenSource;
            if (ts.IsCancellationRequested)
            {
                TokenSource = new CancellationTokenSource();
                ts.Dispose();
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
        }
    }
}
