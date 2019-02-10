namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using ThreadState = System.Threading.ThreadState;

    /// <summary>
    /// Provides a base implementation for application workers
    /// that perform continuous, long-running tasks. This class
    /// provides the ability to perform fine-grained control on these tasks.
    /// </summary>
    /// <seealso cref="IWorker" />
    public abstract class WorkerBase : IWorker
    {
        private readonly object SyncLock = new object();
        private readonly Dictionary<StateChangeRequest, bool> StateChangeRequests;
        private readonly ManualResetEventSlim CycleCompletedEvent = new ManualResetEventSlim(true);
        private readonly ManualResetEventSlim StateChangedEvent = new ManualResetEventSlim(true);

        private readonly Thread Thread;
        private readonly Stopwatch CycleStopwatch = new Stopwatch();

        // Since these are API property backers, we use interlocked to read from them
        // to avoid deadlocked reads
        private long m_Period;
        private int m_IsDisposed = 0;
        private int m_IsDisposing = 0;
        private int m_WorkerState = (int)WorkerState.Created;
        private Task<WorkerState> StateChangeTask;

        // This will be recreated on demand
        private CancellationTokenOwner CycleCancellation = new CancellationTokenOwner();

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerBase"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="priority">The thread priority</param>
        protected WorkerBase(string name, ThreadPriority priority)
        {
            Period = TimeSpan.FromMilliseconds(15);
            Name = name;

            StateChangeRequests = new Dictionary<StateChangeRequest, bool>(5)
            {
                [StateChangeRequest.Start] = false,
                [StateChangeRequest.Pause] = false,
                [StateChangeRequest.Resume] = false,
                [StateChangeRequest.Stop] = false
            };

            Thread = new Thread(RunWorkerLoop)
            {
                IsBackground = true,
                Priority = priority,
                Name = name
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
        public Task<WorkerState> StartAsync()
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
        public Task<WorkerState> PauseAsync()
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
        public Task<WorkerState> ResumeAsync()
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
                return task;
            }
        }

        /// <summary>
        /// Waits for cycle.
        /// </summary>
        /// <param name="millisecondsTimeout">The milliseconds timeout.</param>
        /// <returns>True if the wait was successful</returns>
        public bool Wait(int millisecondsTimeout)
        {
            if (IsDisposing || IsDisposed) return false;
            return CycleCompletedEvent.Wait(millisecondsTimeout);
        }

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Suspends execution queues a new new cycle for execution. The delay is given in
        /// milliseconds. When overridden in a derived class the wait handle will be set
        /// whenever an interrupt is received.
        /// </summary>
        /// <param name="wantedDelay">The remaining delay to wait for in the cycle</param>
        /// <param name="delayTask">Contains a reference to a task with the scheduled period delay</param>
        /// <param name="token">The cancellation token to cancel waiting</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void ExecuteCycleDelay(int wantedDelay, Task delayTask, CancellationToken token)
        {
            if (wantedDelay == 0 || wantedDelay < -1)
                return;

            try { delayTask.Wait(token); } // wantedDelay, token); }
            catch { /* ignore */ }
        }

        /// <summary>
        /// Represents the user defined logic to be executed on a single worker cycle.
        /// Check the cancellation token continuously if you need responsive interrupts.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        protected abstract void ExecuteCycleLogic(CancellationToken ct);

        /// <summary>
        /// Handles the cycle logic exceptions.
        /// </summary>
        /// <param name="ex">The exception that was thrown.</param>
        protected abstract void HandleCycleLogicException(Exception ex);

        /// <summary>
        /// This method is called automatically when <see cref="Dispose()"/> is called.
        /// Makes sure you release all resources within this call.
        /// </summary>
        protected abstract void DisposeManagedState();

        /// <summary>
        /// Implements worker control, execution and delay logic in a loop.
        /// </summary>
        private void RunWorkerLoop()
        {
            while (WorkerState != WorkerState.Stopped)
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
                    HandleCycleLogicException(ex);
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
                        ExecuteCycleDelay(ComputeCycleDelay(initialWorkerState), delayTask, CycleCancellation.Token);
                    }
                }
            }

            ClearStateChangeRequests();
            WorkerState = WorkerState.Stopped;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ComputeCycleDelay(WorkerState initialWorkerState)
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
                    ClearStateChangeRequests();

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

            if (alsoManaged == false) return;

            CycleStopwatch.Stop();
            StateChangedEvent.Dispose();
            CycleCompletedEvent.Dispose();
            CycleCancellation.Dispose();

            if ((Thread.ThreadState & ThreadState.Unstarted) != ThreadState.Unstarted)
                Thread.Join();

            DisposeManagedState();
            IsDisposed = true;

            // There are no unmanaged resources
        }
    }
}
