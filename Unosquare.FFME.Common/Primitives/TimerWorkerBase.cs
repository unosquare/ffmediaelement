namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a base implementation for application workers.
    /// </summary>
    /// <seealso cref="IWorker" />
    public abstract class TimerWorkerBase : WorkerBase
    {
        private readonly object SyncLock = new object();
        private readonly Timer Timer;
        private bool IsTimerAlive = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimerWorkerBase"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="period">The execution interval.</param>
        protected TimerWorkerBase(string name, TimeSpan period)
            : base(name, period)
        {
            // Instantiate the timer that will be used to schedule cycles
            Timer = new Timer(
                ExecuteTimerCallback,
                this,
                Timeout.Infinite,
                Timeout.Infinite);
        }

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
                Interrupt();
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
                Interrupt();
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
                Interrupt();
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
                Interrupt();
                return task;
            }
        }

        /// <summary>
        /// Schedules a new cycle for execution. The delay is given in
        /// milliseconds. Passing a delay of 0 means a new cycle should be executed
        /// immediately.
        /// </summary>
        /// <param name="delay">The delay.</param>
        protected void ScheduleCycle(int delay)
        {
            lock (SyncLock)
            {
                if (!IsTimerAlive) return;
                Timer.Change(delay, Timeout.Infinite);
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool alsoManaged)
        {
            base.Dispose(alsoManaged);

            lock (SyncLock)
            {
                if (!IsTimerAlive) return;
                IsTimerAlive = false;
                Timer.Dispose();
            }
        }

        /// <summary>
        /// Cancels the current token and schedules a new cycle immediately.
        /// </summary>
        private void Interrupt()
        {
            lock (SyncLock)
            {
                if (WorkerState == WorkerState.Stopped)
                    return;

                CycleCancellation.Cancel();
                ScheduleCycle(0);
            }
        }

        /// <summary>
        /// Executes the worker cycle control logic.
        /// This includes processing state change requests,
        /// the exeuction of use cycle code,
        /// and the scheduling of new cycles.
        /// </summary>
        private void ExecuteWorkerCycle()
        {
            CycleStopwatch.Restart();

            lock (SyncLock)
            {
                if (IsDisposing || IsDisposed)
                {
                    WorkerState = WorkerState.Stopped;

                    // Cancel any awaiters
                    try { StateChangedEvent.Set(); }
                    catch { /* Ignore */ }

                    return;
                }

                // Prevent running another instance of the cycle
                if (CycleCompletedEvent.IsSet == false) return;

                // Lock the cycle and capture relevant state valid for this cycle
                CycleCompletedEvent.Reset();
            }

            var interruptToken = CycleCancellation.Token;
            var initialWorkerState = WorkerState;

            // Process the tasks that are awaiting
            if (ProcessStateChangeRequests())
                return;

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

                lock (SyncLock)
                {
                    // Signal the cycle has been completed so new cycles can be executed
                    CycleCompletedEvent.Set();

                    // Schedule a new cycle
                    ScheduleCycle(!interruptToken.IsCancellationRequested
                        ? ComputeCycleDelay(initialWorkerState)
                        : 0);
                }
            }
        }

        /// <summary>
        /// Represents the callback that is executed when the <see cref="Timer"/> ticks.
        /// </summary>
        /// <param name="state">The state -- this contains the wroker.</param>
        private void ExecuteTimerCallback(object state) => ExecuteWorkerCycle();

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

                StateChangeTask = waitingTask;
                StateChangedEvent.Reset();
                StateChangeRequests[request] = true;
                waitingTask.Start();
                CycleCancellation.Cancel();

                return waitingTask;
            }
        }

        /// <summary>
        /// Processes the state change queue by checking pending events and scheduling
        /// cycle execution accordingly. The <see cref="WorkerState"/> is also updated.
        /// </summary>
        /// <returns>Returns <c>true</c> if the execution should be terminated. <c>false</c> otherwise.</returns>
        private bool ProcessStateChangeRequests()
        {
            lock (SyncLock)
            {
                var currentState = WorkerState;
                var hasRequest = false;
                var schedule = 0;

                if (IsDisposing || IsDisposed)
                {
                    hasRequest = true;
                    WorkerState = WorkerState.Stopped;
                }
                else if (StateChangeRequests[StateChangeRequest.Start])
                {
                    hasRequest = true;
                    WorkerState = WorkerState.Waiting;
                }
                else if (StateChangeRequests[StateChangeRequest.Pause])
                {
                    hasRequest = true;
                    WorkerState = WorkerState.Paused;
                    schedule = Timeout.Infinite;
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
                    schedule = Timeout.Infinite;
                }

                // Signals all state changes to continue
                // as a command has been handled.
                if (hasRequest)
                {
                    ClearStateChangeRequests(schedule, currentState, WorkerState);
                }

                return hasRequest;
            }
        }

        /// <summary>
        /// Signals all state change requests to set.
        /// </summary>
        /// <param name="schedule">The cycle schedule.</param>
        /// <param name="oldState">The previosu worker state</param>
        /// <param name="newState">The new worker state</param>
        private void ClearStateChangeRequests(int schedule, WorkerState oldState, WorkerState newState)
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
                OnStateChangeProcessed(oldState, newState);
                ScheduleCycle(schedule);
            }
        }
    }
}