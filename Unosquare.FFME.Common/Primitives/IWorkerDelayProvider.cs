namespace Unosquare.FFME.Primitives
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An interface for a worker cycle delay provider
    /// </summary>
    public interface IWorkerDelayProvider
    {
        /// <summary>
        /// Suspends execution queues a new new cycle for execution. The delay is given in
        /// milliseconds. When overridden in a derived class the wait handle will be set
        /// whenever an interrupt is received.
        /// </summary>
        /// <param name="wantedDelay">The remaining delay to wait for in the cycle</param>
        /// <param name="delayTask">Contains a reference to a task with the scheduled period delay</param>
        /// <param name="token">The cancellation token to cancel waiting</param>
        void ExecuteCycleDelay(int wantedDelay, Task delayTask, CancellationToken token);
    }
}
