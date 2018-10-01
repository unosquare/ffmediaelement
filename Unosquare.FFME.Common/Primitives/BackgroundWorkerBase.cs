namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;

    /// <summary>
    /// A base class used for controlling cyclic execution of logic.
    /// It contains execution rate limiter fuctionality similar to that of a timer
    /// except it does not use the thread pool for queueing cycle execution and prevents
    /// context switching as much as possible.
    /// </summary>
    /// <seealso cref="IDisposable" />
    internal abstract class BackgroundWorkerBase : IDisposable
    {
        #region Private Fields

        // Events: Signalled by the worker thread and waited by public API code.
        private readonly EventWaitHandle WorkerCycledEvent = new EventWaitHandle(false, EventResetMode.ManualReset);
        private readonly EventWaitHandle WorkerStateChangedEvent = new EventWaitHandle(false, EventResetMode.ManualReset);

        // Requests: Signalled by public API code and handled by the worker thread.
        private readonly EventWaitHandle CycleInterruptRequest = new EventWaitHandle(false, EventResetMode.ManualReset);

        // Other state management fields
        private readonly object SyncLock = new object();
        private ThreadState m_WorkerState = ThreadState.Unstarted;
        private ThreadStateRequest m_WorkerStateRequest = ThreadStateRequest.None;
        private volatile bool m_IsCycleInterruptRequested;
        private Thread WorkerThread;
        private bool m_IsDisposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundWorkerBase"/> class.
        /// </summary>
        /// <param name="workerName">Name of the worker.</param>
        /// <param name="priority">The priority.</param>
        /// <param name="cyclePeriod">The cycle period.</param>
        protected BackgroundWorkerBase(string workerName, ThreadPriority priority, TimeSpan cyclePeriod)
        {
            WorkerName = workerName;
            WorkerPriority = priority;
            CyclePeriod = cyclePeriod.TotalMilliseconds < 0 ?
                TimeSpan.Zero : cyclePeriod;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundWorkerBase"/> class.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="cyclePeriod">The cycle period.</param>
        protected BackgroundWorkerBase(ThreadPriority priority, TimeSpan cyclePeriod)
            : this(string.Empty, priority, cyclePeriod)
        {
            WorkerName = GetType().Name;
        }

        #endregion

        #region Enums

        /// <summary>
        /// Enumerates the different state requests
        /// </summary>
        private enum ThreadStateRequest
        {
            /// <summary>No change request</summary>
            None,

            /// <summary>Start request</summary>
            Start,

            /// <summary>Suspend request</summary>
            Suspend,

            /// <summary>Resume request</summary>
            Resume,

            /// <summary>Stop request</summary>
            Stop
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the thread priority of the worker.
        /// </summary>
        public ThreadPriority WorkerPriority { get; }

        /// <summary>
        /// Gets the worker identifier.
        /// </summary>
        public string WorkerName { get; }

        /// <summary>
        /// Gets the cycle period of a standard worker cycle.
        /// This property is used to compute delays for worker cycle executions.
        /// </summary>
        public TimeSpan CyclePeriod { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
        /// </value>
        public bool IsDisposed
        {
            get { lock (SyncLock) return m_IsDisposed; }
            private set { lock (SyncLock) m_IsDisposed = value; }
        }

        /// <summary>
        /// Gets the state of the worker.
        /// </summary>
        public ThreadState WorkerState
        {
            get { lock (SyncLock) return m_WorkerState; }
            private set { lock (SyncLock) m_WorkerState = value; }
        }

        /// <summary>
        /// Gets a value indicating whether a stop or a suspend request has been issued.
        /// The baking value is volatile, and therefore, there is no need for a monitor
        /// or memory barrier to read it.
        /// </summary>
        protected bool IsCycleInterruptRequested
        {
            get => m_IsCycleInterruptRequested;
            private set => m_IsCycleInterruptRequested = value;
        }

        /// <summary>
        /// Gets or sets the requested worker state.
        /// </summary>
        private ThreadStateRequest WorkerStateRequest
        {
            get { lock (SyncLock) return m_WorkerStateRequest; }
            set { lock (SyncLock) m_WorkerStateRequest = value; }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Requests the worker to start running. This method returns immediately.
        /// </summary>
        public void RequestStart() => RequestWorkerState(ThreadStateRequest.Start);

        /// <summary>
        /// Requests the worker to start running. This method waits for the worker to
        /// change to its running state.
        /// </summary>
        public void Start()
        {
            RequestStart();
            WaitForStateChanged();
        }

        /// <summary>
        /// Requests the worker suspends the execution of cycles.
        /// This call returns immediately.
        /// </summary>
        /// <exception cref="ObjectDisposedException">When the worker has been disposed.</exception>
        /// <exception cref="InvalidOperationException">When the worker is not in the running state.</exception>
        public void RequestSuspend() => RequestWorkerState(ThreadStateRequest.Suspend);

        /// <summary>
        /// Requests the worker suspends the execution of cycles and waits for the worker to enter the suspended state.
        /// Call the <see cref="RequestResume"/> method or the <see cref="Stop"/> method to unblock this call.
        /// </summary>
        public void Suspend()
        {
            RequestSuspend();
            WaitForStateChanged();
        }

        /// <summary>
        /// Signals the worker to cancel suspension or delays and continue execution.
        /// This method returns immediately.
        /// </summary>
        /// <exception cref="ObjectDisposedException">When the worker has been disposed.</exception>
        public void RequestResume() => RequestWorkerState(ThreadStateRequest.Resume);

        /// <summary>
        /// Signals the worker to cancel suspension or delays and continue execution.
        /// Waits for a worker cycle to complete.
        /// </summary>
        public void Resume()
        {
            RequestResume();
            WaitForStateChanged();
        }

        /// <summary>
        /// Requests the worker stops the execution of cycles. This method returns immediately.
        /// </summary>
        /// <exception cref="ObjectDisposedException">When the worker has been disposed.</exception>
        /// <exception cref="InvalidOperationException">When the worker is not in the suspended or running state.</exception>
        public void RequestStop() => RequestWorkerState(ThreadStateRequest.Stop);

        /// <summary>
        /// Requests the worker to stop and waits for the worker thread to finish.
        /// </summary>
        public void Stop()
        {
            RequestStop();
            WaitForStateChanged();
        }

        /// <summary>
        /// Waits for a worker cycle to complete.
        /// </summary>
        /// <param name="millisecondsTimeout">The timeout to wait for, in milliseconds. Use a negative value to wait indefinitely</param>
        /// <returns>False if the timeout expired. Otherwise, true.</returns>
        /// <exception cref="ObjectDisposedException">When the worker has been disposed</exception>
        public bool WaitOne(int millisecondsTimeout)
        {
            // Check for disposed
            if (IsDisposed)
                throw new ObjectDisposedException($"Worker '{WorkerName}' has been disposed.");

            var maxTimeout = millisecondsTimeout < 0 ? Timeout.Infinite : millisecondsTimeout;
            return WorkerCycledEvent.WaitOne(maxTimeout, false);
        }

        /// <summary>
        /// Waits for a worker cycle to complete.
        /// </summary>
        /// <param name="timeout">The timeout to wait for. Use a negative values to wait indefinitely</param>
        /// <returns>False if the timeout expired. Otherwise, true.</returns>
        public bool WaitOne(TimeSpan timeout) => WaitOne(Convert.ToInt32(timeout.TotalMilliseconds));

        /// <summary>
        /// Waits indefinitely for a worker cycle to complete
        /// </summary>
        /// <returns>False if the timeout expired. Otherwise, true.</returns>
        public bool WaitOne() => WaitOne(Timeout.Infinite);

        /// <summary>
        /// Waits for the current worker state change request to be completed.
        /// </summary>
        /// <param name="millisecondsTimeout">The timeout to wait for, in milliseconds. Use a negative value to wait indefinitely</param>
        /// <returns>False if the timeout expired. Otherwise, true.</returns>
        /// <exception cref="ObjectDisposedException">When the worker has been disposed</exception>
        public bool WaitForStateChanged(int millisecondsTimeout)
        {
            // Check for disposed
            if (IsDisposed)
                throw new ObjectDisposedException($"Worker '{WorkerName}' has been disposed.");

            var maxTimeout = millisecondsTimeout < 0 ? Timeout.Infinite : millisecondsTimeout;
            return WorkerStateChangedEvent.WaitOne(maxTimeout, false);
        }

        /// <summary>
        /// Waits for the current worker state change request to be completed.
        /// </summary>
        /// <returns>False if the timeout expired. Otherwise, true.</returns>
        public bool WaitForStateChanged() => WaitForStateChanged(Timeout.Infinite);

        /// <summary>
        /// Waits for the current worker state change request to be completed.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>False if the timeout expired. Otherwise, true.</returns>
        public bool WaitForStateChanged(TimeSpan timeout) => WaitForStateChanged(Convert.ToInt32(timeout.TotalMilliseconds));

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            lock (SyncLock)
            {
                if (IsDisposed) return;
                IsDisposed = true;
                CommitWorkerStateRequest(ThreadStateRequest.Stop);
            }

            // Exit and wait for that cleanly
            WorkerThread?.Join();

            // Let the inheriting class do its own cleanup work
            OnWorkerDisposing();

            // Perform cleanup
            WorkerCycledEvent.Dispose();
            WorkerStateChangedEvent.Dispose();
            CycleInterruptRequest.Dispose();
        }

        /// <inheritdoc />
        public override string ToString() =>
            $"{nameof(BackgroundWorkerBase)}: {WorkerName} ({WorkerState})";

        /// <summary>
        /// Contains the logic to be executed within a worker cycle.
        /// Read the <see cref="IsCycleInterruptRequested"/> property to check if the worker cycle
        /// logic needs to be interrupted.
        /// </summary>
        protected abstract void ExecuteWorkerCycle();

        /// <summary>
        /// Delays the worker cycle for the remaining duration of the <see cref="CyclePeriod" />.
        /// Override this method to prevent or enforce delays by choosing to call or not call the base method.
        /// </summary>
        /// <param name="remainingCycleTime">The remaining cycle time.</param>
        protected virtual void DelayWorkerCycle(TimeSpan remainingCycleTime)
        {
            if (remainingCycleTime.TotalMilliseconds <= 0) return;
            CycleInterruptRequest.WaitOne((int)Math.Round(remainingCycleTime.TotalMilliseconds, 0), false);
        }

        /// <summary>
        /// Called when the worker encounters an unhandled exception that forces it to exit.
        /// </summary>
        /// <param name="ex">The unhandled exception.</param>
        protected virtual void OnWorkerException(Exception ex) => throw ex;

        /// <summary>
        /// This method gets called when the worker has started. This is the first bit of
        /// logic that gets executed when the worker enters the run state.
        /// </summary>
        protected virtual void OnWorkerStarted() { }

        /// <summary>
        /// This method gets called when the worker has stopped. This is the last bit of
        /// logic that gets executed before the worker thread gets disposed.
        /// </summary>
        protected virtual void OnWorkerStopped() { }

        /// <summary>
        /// Called when the worker is disposing.
        /// </summary>
        protected virtual void OnWorkerDisposing() { }

        #endregion

        #region Private Methods

        /// <summary>
        /// Commits the worker state request.
        /// </summary>
        /// <param name="request">The request.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CommitWorkerStateRequest(ThreadStateRequest request)
        {
            WorkerStateChangedEvent.Reset();
            WorkerStateRequest = request;
            CycleInterruptRequest.Set();
            IsCycleInterruptRequested = true;
        }

        /// <summary>
        /// Requests a worker state change.
        /// </summary>
        /// <param name="request">The state change request.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RequestWorkerState(ThreadStateRequest request)
        {
            lock (SyncLock)
            {
                // Check for disposed
                if (IsDisposed)
                    throw new ObjectDisposedException($"Worker '{WorkerName}' has been disposed.");

                // If there is already a request of the same type, simply return.
                if (WorkerStateRequest == request)
                    return;

                // We need to have the worker state cleared in order to take in more requests.
                if (WorkerStateRequest != ThreadStateRequest.None)
                    throw new InvalidOperationException($"Unable make a state change request. A '{WorkerStateRequest}' request is pending.");

                switch (request)
                {
                    case ThreadStateRequest.None:
                        throw new ArgumentException($"Worker request '{request}' is invalid.");

                    case ThreadStateRequest.Start:
                        // Ensure the worket thread has not been started
                        if (WorkerThread != null)
                        {
                            throw new InvalidOperationException(
                                $"Worker request to '{nameof(request)}' failed: This request can only be made once.");
                        }

                        // Instantiate the worker thread.
                        WorkerThread = new Thread(RunWorkerThread)
                        {
                            IsBackground = true,
                            Name = WorkerName,
                            Priority = WorkerPriority
                        };

                        WorkerThread.Start();
                        break;

                    default:
                        // Check worker state
                        if (WorkerState == ThreadState.Unstarted || WorkerState == ThreadState.Stopped)
                            throw new InvalidOperationException($"Worker request to '{request}' failed. Worker is not running.");

                        // The only state in which we can start is if we are on the unstarted event
                        if (WorkerThread == null)
                            throw new InvalidOperationException($"Worker request to '{request}' failed. Worker has not been started.");

                        break;
                }

                CommitWorkerStateRequest(request);
            }
        }

        /// <summary>
        /// Updates the state of the worker in a thread critical section.
        /// This method also signals the <see cref="IsCycleInterruptRequested"/> without context switching.
        /// </summary>
        /// <param name="newState">The new state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateWorkerState(ThreadState newState)
        {
            lock (SyncLock)
            {
                switch (newState)
                {
                    case ThreadState.Running:
                        // Initial state for wait handles
                        CycleInterruptRequest.Reset();
                        IsCycleInterruptRequested = false;
                        WorkerCycledEvent.Set();
                        break;

                    case ThreadState.Suspended:
                        // Notify we have suspended and
                        // ensure the interrupt request is not set
                        CycleInterruptRequest.Reset();
                        IsCycleInterruptRequested = false;
                        break;

                    case ThreadState.Stopped:
                        // Prevent any waiting to occur before exit
                        CycleInterruptRequest.Set();
                        IsCycleInterruptRequested = true;
                        WorkerCycledEvent.Set();
                        break;

                    default:
                        throw new NotSupportedException($"Unable to set the worker state to '{newState}'");
                }

                WorkerStateRequest = ThreadStateRequest.None;
                WorkerState = newState;
                WorkerStateChangedEvent.Set();
            }
        }

        /// <summary>
        /// Controls the worker thread loop.
        /// </summary>
        private void RunWorkerThread()
        {
            var executeStopwatch = new System.Diagnostics.Stopwatch();
            var stateRequest = WorkerStateRequest;

            try
            {
                // Call the logic that handles the start.
                UpdateWorkerState(ThreadState.Running);
                OnWorkerStarted();

                // Run until a stop is requested
                while (true)
                {
                    // Mark the begining of a cycle
                    executeStopwatch.Restart();
                    WorkerCycledEvent.Reset();

                    // Process State change requests
                    stateRequest = WorkerStateRequest;

                    if (stateRequest == ThreadStateRequest.Suspend)
                    {
                        UpdateWorkerState(ThreadState.Suspended);
                        CycleInterruptRequest.WaitOne(Timeout.Infinite, false);
                        WorkerCycledEvent.Set();
                        continue;
                    }
                    else if (stateRequest == ThreadStateRequest.Resume)
                    {
                        UpdateWorkerState(ThreadState.Running);
                    }
                    else if (stateRequest == ThreadStateRequest.Stop)
                    {
                        break;
                    }

                    // Execute the cycle logic
                    ExecuteWorkerCycle();

                    // The remaining cycle time is the difference between the target cycle period
                    // and the amount of time the logic took to execute.
                    DelayWorkerCycle(TimeSpan.FromTicks(CyclePeriod.Ticks - executeStopwatch.Elapsed.Ticks));

                    // Signal the end of a cycle
                    WorkerCycledEvent.Set();
                }
            }
            catch (Exception ex)
            {
                OnWorkerException(ex);
            }
            finally
            {
                UpdateWorkerState(ThreadState.Stopped);
                OnWorkerStopped();
            }
        }

        #endregion
    }
}
