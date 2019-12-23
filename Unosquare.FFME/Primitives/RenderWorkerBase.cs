namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A base class for implementing interval workers.
    /// </summary>
    internal abstract class RenderWorkerBase : IWorker
    {
        private readonly object SyncLock = new object();
        private readonly Thread QuantumThread;
        private readonly Stopwatch CycleClock = new Stopwatch();
        private readonly ManualResetEventSlim WantedStateCompleted = new ManualResetEventSlim(true);

        private int m_IsDisposed;
        private int m_IsDisposing;
        private int m_WorkerState = (int)WorkerState.Created;
        private int m_WantedWorkerState = (int)WorkerState.Running;
        private CancellationTokenSource TokenSource = new CancellationTokenSource();

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderWorkerBase"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        protected RenderWorkerBase(string name)
        {
            Name = name;
            QuantumThread = new Thread(RunQuantumThread)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = $"{name}.Thread",
            };
            CycleClock.Restart();
            QuantumThread.Start();
        }

        /// <summary>
        /// Gets the name of the worker.
        /// </summary>
        public string Name { get; }

        /// <inheritdoc />
        public WorkerState WorkerState
        {
            get => (WorkerState)Interlocked.CompareExchange(ref m_WorkerState, 0, 0);
            private set => Interlocked.Exchange(ref m_WorkerState, (int)value);
        }

        /// <inheritdoc />
        public bool IsDisposed
        {
            get => Interlocked.CompareExchange(ref m_IsDisposed, 0, 0) != 0;
            private set => Interlocked.Exchange(ref m_IsDisposed, value ? 1 : 0);
        }

        /// <summary>
        /// Gets a value indicating whether this instance is currently being disposed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is disposing; otherwise, <c>false</c>.
        /// </value>
        protected bool IsDisposing
        {
            get => Interlocked.CompareExchange(ref m_IsDisposing, 0, 0) != 0;
            private set => Interlocked.Exchange(ref m_IsDisposing, value ? 1 : 0);
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
        /// Gets the elapsed time of the last cycle.
        /// </summary>
        protected TimeSpan LastCycleElapsed { get; private set; }

        /// <inheritdoc />
        public Task<WorkerState> StartAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return Task.FromResult(WorkerState);

                if (WorkerState == WorkerState.Created)
                {
                    WantedWorkerState = WorkerState.Running;
                    WorkerState = WorkerState.Running;
                    return Task.FromResult(WorkerState);
                }
                else if (WorkerState == WorkerState.Paused)
                {
                    WantedStateCompleted.Reset();
                    WantedWorkerState = WorkerState.Running;
                }
            }

            return RunWaitForWantedState();
        }

        /// <inheritdoc />
        public Task<WorkerState> PauseAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return Task.FromResult(WorkerState);

                if (WorkerState != WorkerState.Running)
                    return Task.FromResult(WorkerState);

                WantedStateCompleted.Reset();
                WantedWorkerState = WorkerState.Paused;
            }

            return RunWaitForWantedState();
        }

        /// <inheritdoc />
        public Task<WorkerState> ResumeAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return Task.FromResult(WorkerState);

                if (WorkerState != WorkerState.Paused)
                    return Task.FromResult(WorkerState);

                WantedStateCompleted.Reset();
                WantedWorkerState = WorkerState.Running;
            }

            return RunWaitForWantedState();
        }

        /// <inheritdoc />
        public Task<WorkerState> StopAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return Task.FromResult(WorkerState);

                if (WorkerState != WorkerState.Running && WorkerState != WorkerState.Paused)
                    return Task.FromResult(WorkerState);

                WantedStateCompleted.Reset();
                WantedWorkerState = WorkerState.Stopped;
                Interrupt();
            }

            return RunWaitForWantedState();
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
            QuantumThread.Join();

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

        /// <summary>
        /// Returns a hot task that waits for the state of the worker to change.
        /// </summary>
        /// <returns>The awaitable state change task.</returns>
        private Task<WorkerState> RunWaitForWantedState() => Task.Run(() =>
        {
            while (!WantedStateCompleted.Wait(StepTimer.Resolution))
                Interrupt();

            return WorkerState;
        });

        /// <summary>
        /// Called when every quantum of time. Will be called frequently with
        /// a multimedia timer and infrequently with a standard threadin timer.
        /// </summary>
        private void RunQuantumThread(object state)
        {
            using var vsync = new VerticalSyncContext();
            while (true)
            {
                // VerticalSyncContext.Flush();
                vsync.Wait();

                LastCycleElapsed = CycleClock.Elapsed;
                CycleClock.Restart();

                lock (SyncLock)
                {
                    WorkerState = WantedWorkerState;
                    WantedStateCompleted.Set();

                    if (WorkerState == WorkerState.Stopped)
                        break;
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

                vsync.Wait();
            }

            WorkerState = WorkerState.Stopped;
            WantedStateCompleted.Set();
        }
    }
}
