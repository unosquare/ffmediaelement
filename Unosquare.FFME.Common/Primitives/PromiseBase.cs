namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents actions to be performed in the future but that
    /// still are awaitable by other threads.
    /// </summary>
    /// <seealso cref="IDisposable" />
    public abstract class PromiseBase : IDisposable
    {
        #region Private Members

        private readonly object MethodLock = new object();
        private readonly object PropertyLock = new object();
        private readonly ManualResetEventSlim CompletedEvent = new ManualResetEventSlim(false);
        private readonly CancellationTokenSource CancelToken = new CancellationTokenSource();
        private bool m_IsDisposed;
        private bool m_IsExecuting;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PromiseBase"/> class.
        /// </summary>
        /// <param name="continueOnCapturedContext">
        /// if set to <c>true</c> configures the awaiter to continue on the captured context only.
        /// if set to <c>false</c> configures the awaiter to be continued on any thread context.
        /// </param>
        protected PromiseBase(bool continueOnCapturedContext)
        {
            AwaiterTask = new Task<bool>(() =>
            {
                while (CancelToken.IsCancellationRequested == false)
                {
                    if (CompletedEvent.Wait(1))
                        return true;
                }

                return false;
            });

            // We don't start the awaiter just yet.
            // It can be awaited in its created state.
            Awaiter = AwaiterTask.ConfigureAwait(continueOnCapturedContext);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the configured task awaiter.
        /// You should await this object.
        /// The task returns true if the actions were run. Returns false
        /// if the actions were cancelled.
        /// </summary>
        public ConfiguredTaskAwaitable<bool> Awaiter { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        public bool IsDisposed
        {
            get { lock (PropertyLock) return m_IsDisposed; }
            private set { lock (PropertyLock) m_IsDisposed = value; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is running.
        /// </summary>
        public bool IsExecuting
        {
            get { lock (PropertyLock) return m_IsExecuting; }
            private set { lock (PropertyLock) m_IsExecuting = value; }
        }

        /// <summary>
        /// Gets the task that awaits the promise. Do not await on this but use the <see cref="Awaiter"/> property instead.
        /// </summary>
        private Task<bool> AwaiterTask { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Starts to run the promise in a thread pool thread.
        /// </summary>
        public void BeginExecute()
        {
            lock (MethodLock)
            {
                if (IsDisposed || IsExecuting || CompletedEvent.IsSet)
                    return;

                var executeTask = new Task(Execute);
                executeTask.ConfigureAwait(false);
                executeTask.Start();
            }
        }

        /// <summary>
        /// Runs the corresponding actions and completes the Awaiter.
        /// This causes threads awaiting the commands to stop awaiting
        /// </summary>
        public void Execute()
        {
            // We lock to ensure this is called one at a time
            lock (MethodLock)
            {
                try
                {
                    if (IsDisposed || IsExecuting || CompletedEvent.IsSet)
                        return;

                    IsExecuting = true;
                    AwaiterTask.Start();
                    PerformActions();
                }
                finally
                {
                    CompletedEvent.Set();
                    Dispose();
                }
            }
        }

        /// <summary>
        /// Prevents the actions from being run and sets the awaiter as completed.
        /// </summary>
        /// <param name="waitForExit">if set to <c>true</c> it waits for the awaiter to complete synchronously.</param>
        public void Cancel(bool waitForExit)
        {
            lock (MethodLock)
            {
                try
                {
                    if (IsDisposed || IsExecuting || CancelToken.IsCancellationRequested)
                        return;

                    CancelToken.Cancel();
                    AwaiterTask.Start();

                    if (waitForExit)
                        Awaiter.GetAwaiter().GetResult();
                }
                finally { Dispose(); }
            }
        }

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources;
        /// <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool alsoManaged)
        {
            lock (MethodLock)
            {
                if (IsDisposed) return;

                if (alsoManaged == false)
                    return;

                CancelToken.Cancel();
                if (AwaiterTask.Status == TaskStatus.Created)
                    AwaiterTask.Start();

                Awaiter.GetAwaiter().GetResult();

                CancelToken.Dispose();
                CompletedEvent.Dispose();
                AwaiterTask.Dispose();

                IsDisposed = true;
                IsExecuting = false;
            }
        }

        /// <summary>
        /// Performs the actions represented by this deferred task.
        /// </summary>
        protected abstract void PerformActions();

        #endregion
    }
}
