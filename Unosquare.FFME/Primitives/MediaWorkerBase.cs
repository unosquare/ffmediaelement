namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class MediaWorkerBase : IWorker
    {
        private readonly object SyncLock = new object();
        private readonly ManualResetEventSlim WantedStateCompleted = new ManualResetEventSlim(true);
        private readonly MultimediaTimer Timer;
        private CancellationTokenSource TokenSource = new CancellationTokenSource();

        private long m_Period;
        private int m_IsDisposed;
        private int m_IsDisposing;
        private int m_IsRunningCycle;
        private int m_WorkerState = (int)WorkerState.Created;
        private int m_WantedWorkerState = (int)WorkerState.Running;

        protected MediaWorkerBase(string name, TimeSpan period)
        {
            Name = name;
            Period = period;
            Timer = new MultimediaTimer(0, (int)period.TotalMilliseconds);
            Timer.Elapsed += (s, e) => RunCycle();
            Mode = IntervalWorkerMode.Multimedia;
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

        private bool IsRunningCycle
        {
            get => Interlocked.CompareExchange(ref m_IsRunningCycle, 0, 0) != 0;
            set => Interlocked.Exchange(ref m_IsRunningCycle, value ? 1 : 0);
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
                WantedStateCompleted.Set();
                WantedStateCompleted.Dispose();
                TokenSource.Dispose();
                if (Timer != null)
                    Timer.Dispose();

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
        }

        private void RunCycle()
        {
            if (IsRunningCycle)
                return;

            IsRunningCycle = true;

            lock (SyncLock)
            {
                if (WantedWorkerState == WorkerState.Stopped)
                {
                    Timer?.Stop();
                }

                WorkerState = WantedWorkerState;
                WantedStateCompleted.Set();
            }

            if (WorkerState != WorkerState.Running)
            {
                IsRunningCycle = false;
                return;
            }

            try
            {
                var tokenSource = TokenSource;
                if (tokenSource.IsCancellationRequested)
                {
                    tokenSource.Dispose();
                    TokenSource = new CancellationTokenSource();
                }

                ExecuteCycleLogic(TokenSource.Token);
            }
            catch (Exception ex)
            {
                OnCycleException(ex);
            }
            finally
            {
                IsRunningCycle = false;
            }
        }
    }
}
