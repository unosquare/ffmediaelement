namespace Unosquare.FFME.Primitives
{
    using System;
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
        private bool m_IsDisposed = false;
        private bool m_IsExecuting = false;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PromiseBase"/> class.
        /// </summary>
        /// <param name="continueOnCapturedContext">
        /// if set to <c>true</c> configures the awaiter to continue on the captured context.
        /// </param>
        protected PromiseBase(bool continueOnCapturedContext)
        {
            Awaiter = new Task<bool>(() =>
            {
                try
                {
                    CompletedEvent.Wait(CancelToken.Token);
                    return true;
                }
                catch (OperationCanceledException) { return false; }
                catch { throw; }
            });

            // We don't start the awaiter just yet.
            // It can be awaited in its created state.
            Awaiter.ConfigureAwait(continueOnCapturedContext);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the awaitable task.
        /// You should await this object.
        /// The task returns true if the actions were run. Returns false
        /// if the actions were cancelled.
        /// </summary>
        public Task<bool> Awaiter { get; }

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

        #endregion

        #region Methods

        /// <summary>
        /// Starts to run the promise in a threadpool thread.
        /// </summary>
        public void BeginExecute()
        {
            lock (MethodLock)
            {
                if (IsDisposed || IsExecuting || CompletedEvent.IsSet)
                    return;

                ThreadPool.QueueUserWorkItem((s) => Execute());
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
                    Awaiter.Start();
                    PerformActions();
                }
                catch { throw; }
                finally
                {
                    CompletedEvent.Set();
                    Dispose();
                }
            }
        }

        /// <summary>
        /// Prevents the actions from being run and sets the awiter as completed.
        /// </summary>
        /// <param name="waitForExit">if set to <c>true</c> it waits for the awaiter to complete synchronously.</param>
        public void Cancel(bool waitForExit = false)
        {
            lock (MethodLock)
            {
                try
                {
                    if (IsDisposed || IsExecuting || CancelToken.IsCancellationRequested)
                        return;

                    CancelToken.Cancel();
                    Awaiter.Start();

                    if (waitForExit)
                        Awaiter.GetAwaiter().GetResult();
                }
                catch { throw; }
                finally { Dispose(); }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
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
                if (Awaiter.Status == TaskStatus.Created)
                    Awaiter.Start();

                Awaiter.GetAwaiter().GetResult();

                CancelToken.Dispose();
                CompletedEvent.Dispose();
                Awaiter.Dispose();

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
