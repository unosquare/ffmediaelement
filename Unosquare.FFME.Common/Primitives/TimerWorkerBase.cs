namespace Unosquare.FFME.Primitives
{
    using System.Threading;

    /// <summary>
    /// A base class for implementing workers that schedule cycles
    /// using a <see cref="Timer"/> and execute logic in a <see cref="ThreadPool"/> thread.
    /// The scheduling prevents overlapping execution of logic.
    /// </summary>
    public abstract class TimerWorkerBase : WorkerBase
    {
        private readonly Timer Timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimerWorkerBase"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        protected TimerWorkerBase(string name)
            : base(name)
        {
            // Instantiate the timer that will be used to schedule cycles
            Timer = new Timer(
                ExecuteTimerCallback,
                this,
                Timeout.Infinite,
                Timeout.Infinite);
        }

        /// <inheritdoc />
        protected override void ScheduleCycle(int delay) => Timer.Change(delay, Timeout.Infinite);

        /// <inheritdoc />
        protected override void DisposeManagedState() => Timer.Dispose();

        /// <summary>
        /// Represents the callback that is executed when the <see cref="Timer"/> ticks.
        /// </summary>
        /// <param name="state">The state -- this contains the wroker.</param>
        private void ExecuteTimerCallback(object state) => ExecuteWorkerCycle();
    }
}
