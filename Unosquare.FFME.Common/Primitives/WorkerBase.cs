namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a base implementation for application workers.
    /// </summary>
    /// <seealso cref="IWorker" />
    public abstract class WorkerBase : IWorker
    {
        private readonly object SyncLock = new object();
        private readonly Dictionary<StateChangeRequest, Task<WorkerState>> StateChangeTasks;
        private readonly Dictionary<StateChangeRequest, ManualResetEventSlim> StateChangeEvents;
        private readonly ManualResetEventSlim CycleCompletedEvent = new ManualResetEventSlim(true);
        private readonly Stopwatch CycleStopwatch = new Stopwatch();

        // Since these are API property backers, we use interlocked to read from them
        // to avoid deadlocked reads
        private long m_Period;
        private int m_IsDisposed = 0;
        private int m_IsDisposing = 0;
        private int m_WorkerState = (int)WorkerState.Created;

        // This will be recreated on demand
        private CancellationTokenSource InterruptTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerBase"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        protected WorkerBase(string name)
        {
            Period = TimeSpan.FromMilliseconds(15);

            StateChangeTasks = new Dictionary<StateChangeRequest, Task<WorkerState>>(5)
            {
                [StateChangeRequest.Start] = null,
                [StateChangeRequest.Pause] = null,
                [StateChangeRequest.Resume] = null,
                [StateChangeRequest.Stop] = null
            };

            StateChangeEvents = new Dictionary<StateChangeRequest, ManualResetEventSlim>(5)
            {
                [StateChangeRequest.Start] = new ManualResetEventSlim(false),
                [StateChangeRequest.Pause] = new ManualResetEventSlim(true),
                [StateChangeRequest.Resume] = new ManualResetEventSlim(true),
                [StateChangeRequest.Stop] = new ManualResetEventSlim(true)
            };
        }

        /// <summary>
        /// Enumerates all the different state change requests
        /// </summary>
        private enum StateChangeRequest
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
            private set => Interlocked.Exchange(ref m_WorkerState, (int)value);
        }

        /// <inheritdoc />
        public bool IsDisposed
        {
            get => Interlocked.CompareExchange(ref m_IsDisposed, 0, 0) != 0;
            private set => Interlocked.Exchange(ref m_IsDisposed, value ? 1 : 0);
        }

        /// <inheritdoc />
        public bool IsDisposing
        {
            get => Interlocked.CompareExchange(ref m_IsDisposing, 0, 0) != 0;
            private set => Interlocked.Exchange(ref m_IsDisposing, value ? 1 : 0);
        }

        /// <inheritdoc />
        public void Interrupt()
        {
            lock (SyncLock)
            {
                if (WorkerState != WorkerState.Running && WorkerState != WorkerState.Waiting && WorkerState != WorkerState.Paused)
                    return;

                if (InterruptTokenSource.IsCancellationRequested)
                    return;

                if (CycleCompletedEvent.IsSet)
                {
                    ScheduleCycle(0);
                }
                else
                {
                    InterruptTokenSource.Cancel();
                }
            }
        }

        /// <inheritdoc />
        public Task<WorkerState> StartAsync()
        {
            lock (SyncLock)
            {
                if (WorkerState == WorkerState.Paused)
                    return ResumeAsync();

                if (WorkerState != WorkerState.Created)
                    return Task.FromResult(WorkerState);

                var task = QueueStateChange(StateChangeRequest.Start);
                ScheduleCycle(0);
                return task;
            }
        }

        /// <inheritdoc />
        public Task<WorkerState> PauseAsync(bool interrupt)
        {
            lock (SyncLock)
            {
                if (WorkerState != WorkerState.Running && WorkerState != WorkerState.Waiting)
                    return Task.FromResult(WorkerState);

                var task = QueueStateChange(StateChangeRequest.Pause);
                if (interrupt) Interrupt();
                return task;
            }
        }

        /// <inheritdoc />
        public Task<WorkerState> ResumeAsync()
        {
            lock (SyncLock)
            {
                if (WorkerState == WorkerState.Created)
                    return StartAsync();

                if (WorkerState != WorkerState.Paused)
                    return Task.FromResult(WorkerState);

                var task = QueueStateChange(StateChangeRequest.Resume);
                ScheduleCycle(0);
                return task;
            }
        }

        /// <inheritdoc />
        public Task<WorkerState> StopAsync()
        {
            lock (SyncLock)
            {
                if (WorkerState == WorkerState.Stopped || WorkerState == WorkerState.Created)
                {
                    WorkerState = WorkerState.Stopped;
                    return Task.FromResult(WorkerState);
                }

                var task = QueueStateChange(StateChangeRequest.Stop);
                Interrupt();
                ScheduleCycle(0);
                return task;
            }
        }

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Schedules a new cycle for execution. The delay is given in
        /// milliseconds. Passing a delay of 0 means a new cycle should be executed
        /// immediately.
        /// </summary>
        /// <param name="delay">The delay.</param>
        protected abstract void ScheduleCycle(int delay);

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
        protected abstract void DisposeManagedState();

        /// <summary>
        /// Executes the worker cycle control logic.
        /// This includes processing state change requests,
        /// the exeuction of <see cref="ExecuteCycleLogic(CancellationToken)"/>
        /// and the scheduling of new cycles.
        /// </summary>
        protected void ExecuteWorkerCycle()
        {
            var initialWorkerState = WorkerState.Created;

            lock (SyncLock)
            {
                // don't run the cycle if we have not completed
                if (IsDisposed || CycleCompletedEvent.IsSet == false)
                    return;

                // Capture the state and restart the cycle timer
                initialWorkerState = WorkerState;
                CycleStopwatch.Restart();

                // Lock the cycle and immediately lock via event
                CycleCompletedEvent.Reset();

                // Process the tasks that are awaiting
                if (ProcessStateChangeQueue())
                    return;

                // Mark the state as Running
                WorkerState = WorkerState.Running;
            }

            try
            {
                // Call the execution logic
                if (InterruptTokenSource.IsCancellationRequested == false)
                    ExecuteCycleLogic(InterruptTokenSource.Token);
            }
            finally
            {
                lock (SyncLock)
                {
                    // Recover the interrupt token if it was used up.
                    var interruptHandled = InterruptTokenSource.IsCancellationRequested;
                    if (interruptHandled)
                    {
                        InterruptTokenSource.Dispose();
                        InterruptTokenSource = new CancellationTokenSource();
                    }

                    // Schedule callback execution via change
                    var millisDifference = Period.TotalMilliseconds - CycleStopwatch.ElapsedMilliseconds;
                    var pauseTimeMilliseconds = millisDifference >= int.MaxValue ? int.MaxValue : Convert.ToInt32(millisDifference);
                    if (pauseTimeMilliseconds <= 0) pauseTimeMilliseconds = 0;
                    if (initialWorkerState == WorkerState.Paused || Period == TimeSpan.MaxValue) pauseTimeMilliseconds = Timeout.Infinite;
                    if (interruptHandled) pauseTimeMilliseconds = 0;

                    // Update the state
                    WorkerState = initialWorkerState == WorkerState.Paused
                        ? WorkerState.Paused
                        : WorkerState.Waiting;

                    // Signal the cycle has been completed so new cycles can be executed
                    CycleCompletedEvent.Set();

                    // Schedule a new cycle
                    ScheduleCycle(pauseTimeMilliseconds);
                }
            }
        }

        /// <summary>
        /// Queues a transition in worker state for processing. Returns a task that can be awaited
        /// when the operation completes.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The awaitable task.</returns>
        private Task<WorkerState> QueueStateChange(StateChangeRequest request)
        {
            lock (SyncLock)
            {
                if (StateChangeTasks[request] != null)
                    return StateChangeTasks[request];

                var task = new Task<WorkerState>(() =>
                {
                    ManualResetEventSlim changeEvent = null;
                    lock (SyncLock)
                        changeEvent = StateChangeEvents[request];

                    changeEvent?.Wait();
                    lock (SyncLock)
                    {
                        StateChangeTasks[request] = null;
                        return WorkerState;
                    }
                });

                task.ConfigureAwait(false);
                StateChangeTasks[request] = task;
                StateChangeEvents[request].Reset();

                task.Start();
                return task;
            }
        }

        /// <summary>
        /// Processes the state change queue by checking pending events and scheduling
        /// cycle execution accordingly. The <see cref="WorkerState"/> is also updated.
        /// </summary>
        /// <returns>Returns <c>true</c> if the execution should be terminated. <c>false</c> otherwise.</returns>
        private bool ProcessStateChangeQueue()
        {
            lock (SyncLock)
            {
                var changeRequest = StateChangeRequest.None;

                if (StateChangeEvents[StateChangeRequest.Start].IsSet == false)
                {
                    changeRequest = StateChangeRequest.Start;
                    WorkerState = WorkerState.Waiting;
                    CycleCompletedEvent.Set();
                    ScheduleCycle(0);
                }
                else if (StateChangeEvents[StateChangeRequest.Pause].IsSet == false)
                {
                    changeRequest = StateChangeRequest.Pause;
                    WorkerState = WorkerState.Paused;
                    CycleCompletedEvent.Set();
                    ScheduleCycle(Timeout.Infinite);
                }
                else if (StateChangeEvents[StateChangeRequest.Resume].IsSet == false)
                {
                    changeRequest = StateChangeRequest.Resume;
                    WorkerState = WorkerState.Waiting;
                    CycleCompletedEvent.Set();
                    ScheduleCycle(0);
                }
                else if (StateChangeEvents[StateChangeRequest.Stop].IsSet == false)
                {
                    changeRequest = StateChangeRequest.Stop;
                    WorkerState = WorkerState.Stopped;
                    CycleCompletedEvent.Set();
                    ScheduleCycle(Timeout.Infinite);
                }

                // Signals all state changes to continue
                // as a command has been handled.
                if (changeRequest != StateChangeRequest.None)
                    ClearStateChangeQueue();

                return changeRequest != StateChangeRequest.None;
            }
        }

        /// <summary>
        /// Signals all state change requests to set.
        /// </summary>
        private void ClearStateChangeQueue()
        {
            // Mark all events as completed
            StateChangeEvents[StateChangeRequest.Start].Set();
            StateChangeEvents[StateChangeRequest.Pause].Set();
            StateChangeEvents[StateChangeRequest.Resume].Set();
            StateChangeEvents[StateChangeRequest.Stop].Set();
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing) return;
                IsDisposing = true;
            }

            // This also ensures the state change queue gets cleared
            StopAsync().GetAwaiter().GetResult();

            if (alsoManaged)
            {
                foreach (var kvp in StateChangeEvents)
                {
                    kvp.Value.Set();
                    kvp.Value.Dispose();
                }

                CycleCompletedEvent.Set();
                CycleCompletedEvent.Dispose();
                CycleStopwatch.Stop();
                InterruptTokenSource.Dispose();

                DisposeManagedState();
                IsDisposed = true;
            }

            // There are no unmanaged resources
        }
    }
}
