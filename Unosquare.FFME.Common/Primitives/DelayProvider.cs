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
        private readonly Action DelayAction;
        private readonly Stopwatch DelayStopwatch = new Stopwatch();
        private bool IsDisposed;
        private IWaitEvent DelayEvent;

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
            /// Using a wait event that completes in a background thread pool thread.
            /// </summary>
            ThreadPool
        }

        /// <summary>
        /// Gets the selected delay strategy.
        /// </summary>
        public DelayStrategy Strategy { get; }

        /// <summary>
        /// Creates the smallest possible, synchronous delay based on the selected strategy
        /// </summary>
        /// <returns>The elapsed time of the delay</returns>
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

        /// <inheritdoc />
        public void Dispose()
        {
            lock (SyncRoot)
            {
                if (IsDisposed) return;
                IsDisposed = true;
                DelayEvent?.Dispose();
                DelayStopwatch.Stop();
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
            lock (SyncRoot)
            {
                if (DelayEvent == null)
                    DelayEvent = WaitEventFactory.Create(isCompleted: true, useSlim: true);
            }

            DelayEvent.Begin();
            ThreadPool.QueueUserWorkItem(s =>
            {
                DelaySleep();
                DelayEvent.Complete();
            });

            DelayEvent.Wait();
        }

        #endregion
    }
}