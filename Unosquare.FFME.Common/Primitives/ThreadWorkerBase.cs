namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ThreadState = System.Threading.ThreadState;

    /// <summary>
    /// Provides a base implementation for application workers
    /// that perform continuous, long-running tasks. This class
    /// provides the ability to perform fine-grained control on these tasks.
    /// </summary>
    /// <seealso cref="IWorker" />
    public abstract class ThreadWorkerBase : WorkerBase
    {
        private readonly object SyncLock = new object();
        private readonly Thread Thread;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadWorkerBase"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="priority">The thread priority</param>
        /// <param name="period">The interval of cycle execution</param>
        /// <param name="delayProvider">The cycle delay provide implementation</param>
        protected ThreadWorkerBase(string name, ThreadPriority priority, TimeSpan period, IWorkerDelayProvider delayProvider)
            : base(name, period)
        {
            DelayProvider = delayProvider;
            Thread = new Thread(RunWorkerLoop)
            {
                IsBackground = true,
                Priority = priority,
                Name = name
            };
        }

        /// <summary>
        /// Provides an implementation on a cycle delay provider.
        /// </summary>
        protected IWorkerDelayProvider DelayProvider { get; }

        /// <inheritdoc />
        public override Task<WorkerState> StartAsync()
        {
            lock (SyncLock)
            {
                if (WorkerState == WorkerState.Paused || WorkerState == WorkerState.Waiting)
                    return ResumeAsync();

                if (WorkerState != WorkerState.Created)
                    return Task.FromResult(WorkerState);

                var task = QueueStateChange(StateChangeRequest.Start);
                Thread.Start();
                return task;
            }
        }

        /// <inheritdoc />
        public override Task<WorkerState> PauseAsync()
        {
            lock (SyncLock)
            {
                if (WorkerState != WorkerState.Running && WorkerState != WorkerState.Waiting)
                    return Task.FromResult(WorkerState);

                var task = QueueStateChange(StateChangeRequest.Pause);
                return task;
            }
        }

        /// <inheritdoc />
        public override Task<WorkerState> ResumeAsync()
        {
            lock (SyncLock)
            {
                if (WorkerState == WorkerState.Created)
                    return StartAsync();

                if (WorkerState != WorkerState.Paused && WorkerState != WorkerState.Waiting)
                    return Task.FromResult(WorkerState);

                var task = QueueStateChange(StateChangeRequest.Resume);
                return task;
            }
        }

        /// <inheritdoc />
        public override Task<WorkerState> StopAsync()
        {
            lock (SyncLock)
            {
                if (WorkerState == WorkerState.Stopped || WorkerState == WorkerState.Created)
                {
                    WorkerState = WorkerState.Stopped;
                    return Task.FromResult(WorkerState);
                }

                var task = QueueStateChange(StateChangeRequest.Stop);
                return task;
            }
        }

        /// <summary>
        /// Suspends execution queues a new new cycle for execution. The delay is given in
        /// milliseconds. When overridden in a derived class the wait handle will be set
        /// whenever an interrupt is received.
        /// </summary>
        /// <param name="wantedDelay">The remaining delay to wait for in the cycle</param>
        /// <param name="delayTask">Contains a reference to a task with the scheduled period delay</param>
        /// <param name="token">The cancellation token to cancel waiting</param>
        protected virtual void ExecuteCycleDelay(int wantedDelay, Task delayTask, CancellationToken token) =>
            DelayProvider?.ExecuteCycleDelay(wantedDelay, delayTask, token);

        /// <inheritdoc />
        protected override void OnDisposing()
        {
            if ((Thread.ThreadState & ThreadState.Unstarted) != ThreadState.Unstarted)
                Thread.Join();
        }

        /// <summary>
        /// Implements worker control, execution and delay logic in a loop.
        /// </summary>
        private void RunWorkerLoop()
        {
            while (WorkerState != WorkerState.Stopped && !IsDisposing)
            {
                CycleStopwatch.Restart();
                var interruptToken = CycleCancellation.Token;
                var period = Period.TotalMilliseconds >= int.MaxValue ? -1 : Convert.ToInt32(Math.Floor(Period.TotalMilliseconds));
                var delayTask = Task.Delay(period, interruptToken);
                var initialWorkerState = WorkerState;

                // Lock the cycle and capture relevant state valid for this cycle
                CycleCompletedEvent.Reset();

                // Process the tasks that are awaiting
                if (ProcessStateChangeRequests())
                    continue;

                try
                {
                    if (initialWorkerState == WorkerState.Waiting &&
                        !interruptToken.IsCancellationRequested)
                    {
                        // Mark the state as Running
                        WorkerState = WorkerState.Running;

                        // Call the execution logic
                        ExecuteCycleLogic(interruptToken);
                    }
                }
                catch (Exception ex)
                {
                    OnCycleException(ex);
                }
                finally
                {
                    // Update the state
                    WorkerState = initialWorkerState == WorkerState.Paused
                        ? WorkerState.Paused
                        : WorkerState.Waiting;

                    // Signal the cycle has been completed so new cycles can be executed
                    CycleCompletedEvent.Set();

                    if (!interruptToken.IsCancellationRequested)
                    {
                        var cycleDelay = ComputeCycleDelay(initialWorkerState);
                        if (cycleDelay == Timeout.Infinite)
                            delayTask = Task.Delay(Timeout.Infinite, interruptToken);

                        ExecuteCycleDelay(
                            cycleDelay,
                            delayTask,
                            CycleCancellation.Token);
                    }
                }
            }

            ClearStateChangeRequests();
            WorkerState = WorkerState.Stopped;
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
                if (StateChangeTask != null)
                    return StateChangeTask;

                var waitingTask = new Task<WorkerState>(() =>
                {
                    StateChangedEvent.Wait();
                    lock (SyncLock)
                    {
                        StateChangeTask = null;
                        return WorkerState;
                    }
                });

                waitingTask.ConfigureAwait(false);
                StateChangeTask = waitingTask;
                StateChangedEvent.Reset();
                StateChangeRequests[request] = true;
                waitingTask.Start();
                CycleCancellation.Cancel();

                return waitingTask;
            }
        }

        /// <summary>
        /// Processes the state change request by checking pending events and scheduling
        /// cycle execution accordingly. The <see cref="WorkerState"/> is also updated.
        /// </summary>
        /// <returns>Returns <c>true</c> if the execution should be terminated. <c>false</c> otherwise.</returns>
        private bool ProcessStateChangeRequests()
        {
            lock (SyncLock)
            {
                var hasRequest = false;
                var currentState = WorkerState;

                if (StateChangeRequests[StateChangeRequest.Start])
                {
                    hasRequest = true;
                    WorkerState = WorkerState.Waiting;
                }
                else if (StateChangeRequests[StateChangeRequest.Pause])
                {
                    hasRequest = true;
                    WorkerState = WorkerState.Paused;
                }
                else if (StateChangeRequests[StateChangeRequest.Resume])
                {
                    hasRequest = true;
                    WorkerState = WorkerState.Waiting;
                }
                else if (StateChangeRequests[StateChangeRequest.Stop])
                {
                    hasRequest = true;
                    WorkerState = WorkerState.Stopped;
                }

                // Signals all state changes to continue
                // as a command has been handled.
                if (hasRequest)
                {
                    ClearStateChangeRequests();
                    OnStateChangeProcessed(currentState, WorkerState);
                }

                return hasRequest;
            }
        }

        /// <summary>
        /// Signals all state change requests to set.
        /// </summary>
        private void ClearStateChangeRequests()
        {
            lock (SyncLock)
            {
                // Mark all events as completed
                StateChangeRequests[StateChangeRequest.Start] = false;
                StateChangeRequests[StateChangeRequest.Pause] = false;
                StateChangeRequests[StateChangeRequest.Resume] = false;
                StateChangeRequests[StateChangeRequest.Stop] = false;

                StateChangedEvent.Set();
                CycleCompletedEvent.Set();
            }
        }
    }
}
