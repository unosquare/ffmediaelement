namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents logic providing several delay mechanisms
    /// </summary>
    public sealed class DelayProvider : IDisposable
    {
        private readonly object SyncRoot = new object();
        private readonly Action DelayAction = null;
        private bool IsDisposed = false;
        private IWaitEvent DelayEvent = null;
        private Stopwatch DelayStopwatch = new Stopwatch();

        /// <summary>
        /// Initializes a new instance of the <see cref="DelayProvider"/> class.
        /// </summary>
        /// <param name="strategy">The strategy.</param>
        public DelayProvider(DelayStrategy strategy)
        {
            Strategy = strategy;
            switch (Strategy)
            {
                case DelayStrategy.ThreadSleep:
                    DelayAction = DelaySleep;
                    break;
                case DelayStrategy.TaskDelay:
                    DelayAction = DelayTask;
                    break;
                case DelayStrategy.ThreadPool:
                    DelayAction = DelayThreadPool;
                    break;
                default:
                    throw new ArgumentException($"{nameof(strategy)} is invalid");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelayProvider"/> class.
        /// </summary>
        public DelayProvider()
            : this(DelayStrategy.TaskDelay)
        {
            // placeholder
        }

        /// <summary>
        /// Enumerates the different ways of providing delays
        /// </summary>
        public enum DelayStrategy
        {
            /// <summary>
            /// Using the Thread.Sleep(1) mechanism
            /// </summary>
            ThreadSleep,

            /// <summary>
            /// Using the Task.Delay(1).Wait mechanism
            /// </summary>
            TaskDelay,

            /// <summary>
            /// Using a wait event that completes in a background threadpool thread.
            /// </summary>
            ThreadPool
        }

        /// <summary>
        /// Gets the selected delay strategy.
        /// </summary>
        public DelayStrategy Strategy { get; private set; }

        /// <summary>
        /// Creates the smallest possible, synchronous delay based on the selected strategy
        /// </summary>
        /// <returns>The elamped time of the delay</returns>
        public TimeSpan WaitOne()
        {
            lock (SyncRoot)
            {
                if (IsDisposed) return TimeSpan.Zero;

                DelayStopwatch.Restart();
                DelayAction();
                return DelayStopwatch.Elapsed;
            }
        }

        #region Dispose Pattern

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            lock (SyncRoot)
            {
                if (IsDisposed) return;
                IsDisposed = true;
                DelayEvent?.Dispose();
            }
        }

        #endregion

        #region Private Delay Mechanisms

        /// <summary>
        /// Implementation using Thread.Sleep
        /// </summary>
        private void DelaySleep()
        {
            Thread.Sleep(10);
        }

        /// <summary>
        /// Implementation using Task.Delay
        /// </summary>
        private void DelayTask()
        {
            Task.Delay(1).ConfigureAwait(continueOnCapturedContext: false)
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// Implementation using the ThreadPool with a wait event.
        /// </summary>
        private void DelayThreadPool()
        {
            if (DelayEvent == null)
                DelayEvent = WaitEventFactory.Create(isCompleted: true, useSlim: true);

            DelayEvent.Begin();
            ThreadPool.QueueUserWorkItem((s) =>
            {
                DelaySleep();
                DelayEvent.Complete();
            });

            DelayEvent.Wait();
        }

        #endregion
    }
}