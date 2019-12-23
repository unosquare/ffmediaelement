namespace Unosquare.FFME.Primitives
{
    /// <summary>
    /// A base class for implementing interval workers.
    /// </summary>
    internal abstract class IntervalWorkerBase : WorkerBase
    {
        private readonly StepTimer QuantumTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntervalWorkerBase"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        protected IntervalWorkerBase(string name)
            : base(name)
        {
            QuantumTimer = new StepTimer(OnQuantumTicked);
        }

        /// <inheritdoc />
        protected override void Dispose(bool alsoManaged)
        {
            base.Dispose(alsoManaged);
            QuantumTimer.Dispose();
        }

        /// <summary>
        /// Called when every quantum of time occurs.
        /// </summary>
        private void OnQuantumTicked()
        {
            if (!TryBeginCycle())
                return;

            ExecuteCyle();
        }
    }
}
