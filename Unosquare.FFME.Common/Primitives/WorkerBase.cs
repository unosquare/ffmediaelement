namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides base infrastructure for Timer and Thread workers.
    /// </summary>
    /// <seealso cref="IWorker" />
    public abstract class WorkerBase : IWorker
    {
        // Since these are API property backers, we use interlocked to read from them
        // to avoid deadlocked reads
        private readonly object SyncLock = new object();
        private long m_Period;
        private int m_IsDisposed;
        private int m_IsDisposing;
        private int m_WorkerState = (int)WorkerState.Created;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerBase"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="period">The execution interval.</param>
        protected WorkerBase(string name, TimeSpan period)
        {
            Name = name;
            Period = period;

            StateChangeRequests = new Dictionary<StateChangeRequest, bool>(5)
            {
                [StateChangeRequest.Start] = false,
                [StateChangeRequest.Pause] = false,
                [StateChangeRequest.Resume] = false,
                [StateChangeRequest.Stop] = false
            };
        }

        /// <summary>
        /// Enumerates all the different state change requests
        /// </summary>
        protected enum StateChangeRequest
        {
            /// <summary>
            /// No state change request.
            /// </summary>
            None,

            /// <summary>
            /// Start state change request
            /// </summary>
            Start,

            /// <summary>
            /// Pause state change request
            /// </summary>
            Pause,

            /// <summary>
            /// Resume state change request
            /// </summary>
            Resume,

            /// <summary>
            /// Stop state change request
            /// </summary>
            Stop
        }

        /// <inheritdoc />
        public string Name { get; }

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
        /// Gets the default period of 15 milliseconds which is the default precision for timers.
        /// </summary>
        protected static TimeSpan DefaultPeriod { get; } = TimeSpan.FromMilliseconds(15);

        /// <summary>
        /// Gets a value indicating whether stop has been requested.
        /// This is useful to prevent more requests from being issued.
        /// </summary>
        protected bool IsStopRequested => StateChangeRequests[StateChangeRequest.Stop];

        /// <summary>
        /// Gets the cycle stopwatch.
        /// </summary>
        protected Stopwatch CycleStopwatch { get; } = new Stopwatch();

        /// <summary>
        /// Gets the state change requests.
        /// </summary>
        protected Dictionary<StateChangeRequest, bool> StateChangeRequests { get; }

        /// <summary>
        /// Gets the cycle completed event.
        /// </summary>
        protected ManualResetEventSlim CycleCompletedEvent { get; } = new ManualResetEventSlim(true);

        /// <summary>
        /// Gets the state changed event.
        /// </summary>
        protected ManualResetEventSlim StateChangedEvent { get; } = new ManualResetEventSlim(true);

        /// <summary>
        /// Gets the cycle logic cancellation owner.
        /// </summary>
        protected CancellationTokenOwner CycleCancellation { get; } = new CancellationTokenOwner();

        /// <summary>
        /// Gets or sets the state change task.
        /// </summary>
        protected Task<WorkerState> StateChangeTask { get; set; }

        /// <inheritdoc />
        public abstract Task<WorkerState> StartAsync();

        /// <inheritdoc />
        public abstract Task<WorkerState> PauseAsync();

        /// <inheritdoc />
        public abstract Task<WorkerState> ResumeAsync();

        /// <inheritdoc />
        public abstract Task<WorkerState> StopAsync();

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool alsoManaged)
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing) return;
                IsDisposing = true;
            }

            // This also ensures the state change queue gets cleared
            StopAsync().Wait();
            StateChangedEvent.Set();
            CycleCompletedEvent.Set();

            OnDisposing();

            CycleStopwatch.Stop();
            StateChangedEvent.Dispose();
            CycleCompletedEvent.Dispose();
            CycleCancellation.Dispose();

            IsDisposed = true;
            IsDisposing = false;
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
        /// Called when a state change request is processed.
        /// </summary>
        /// <param name="previousState">The state befor the change.</param>
        /// <param name="newState">The new state.</param>
        protected virtual void OnStateChangeProcessed(WorkerState previousState, WorkerState newState)
        {
            // placeholder
        }

        /// <summary>
        /// Computes the cycle delay.
        /// </summary>
        /// <param name="initialWorkerState">Initial state of the worker.</param>
        /// <returns>The number of milliseconds to delay for</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected int ComputeCycleDelay(WorkerState initialWorkerState)
        {
            var delay = 0;
            var elapsedMillis = CycleStopwatch.ElapsedMilliseconds;
            var period = Period;
            var periodMillis = period.TotalMilliseconds;
            var delayMillis = periodMillis - elapsedMillis;

            if (initialWorkerState == WorkerState.Paused || period == TimeSpan.MaxValue || delayMillis >= int.MaxValue)
                delay = Timeout.Infinite;
            else if (elapsedMillis >= periodMillis)
                delay = 0;
            else
                delay = Convert.ToInt32(Math.Floor(delayMillis));

            return delay;
        }
    }
}
