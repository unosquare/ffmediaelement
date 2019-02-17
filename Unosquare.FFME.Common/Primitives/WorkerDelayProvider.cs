namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a class that implements delay logic for thread workers
    /// </summary>
    public static class WorkerDelayProvider
    {
        /// <summary>
        /// Provides a delay implementation which simply waits on the task and cancels on
        /// the cancelation token.
        /// </summary>
        public static IWorkerDelayProvider Token => new TokenCancellableDelay();

        /// <summary>
        /// Provides a delay implementation which waits on the task and cancels on both,
        /// the cancelation token and a wanted delay timeout.
        /// </summary>
        public static IWorkerDelayProvider TokenTimeout => new TokenTimeoutCancellableDelay();

        /// <summary>
        /// Provides a delay implementation which uses short sleep intervals of 5ms.
        /// </summary>
        public static IWorkerDelayProvider TokenSleep => new TokenSleepeDelay();

        /// <summary>
        /// Provides a delay implementation which uses short sleep intervals of 5ms and
        /// a wait on the delay task in the final loop.
        /// </summary>
        public static IWorkerDelayProvider PrecisionTokenSleep => new PrecisionTokenSleepeDelay();

        private class TokenCancellableDelay : IWorkerDelayProvider
        {
            public void ExecuteCycleDelay(int wantedDelay, Task delayTask, CancellationToken token)
            {
                if (wantedDelay == 0 || wantedDelay < -1)
                    return;

                // only wait on the cancellation token
                // or until the task completes normally
                try { delayTask.Wait(token); }
                catch { /* ignore */ }
            }
        }

        private class TokenTimeoutCancellableDelay : IWorkerDelayProvider
        {
            public void ExecuteCycleDelay(int wantedDelay, Task delayTask, CancellationToken token)
            {
                if (wantedDelay == 0 || wantedDelay < -1)
                    return;

                try { delayTask.Wait(wantedDelay, token); }
                catch { /* ignore */ }
            }
        }

        private class TokenSleepeDelay : IWorkerDelayProvider
        {
            private readonly Stopwatch ElapsedWait = new Stopwatch();

            public void ExecuteCycleDelay(int wantedDelay, Task delayTask, CancellationToken token)
            {
                ElapsedWait.Restart();

                if (wantedDelay == 0 || wantedDelay < -1)
                    return;

                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(5);

                    if (wantedDelay != Timeout.Infinite && ElapsedWait.ElapsedMilliseconds >= wantedDelay)
                        break;
                }
            }
        }

        private class PrecisionTokenSleepeDelay : IWorkerDelayProvider
        {
            private readonly Stopwatch ElapsedWait = new Stopwatch();

            public void ExecuteCycleDelay(int wantedDelay, Task delayTask, CancellationToken token)
            {
                ElapsedWait.Restart();

                if (wantedDelay == 0 || wantedDelay < -1)
                    return;

                if (wantedDelay == Timeout.Infinite)
                {
                    delayTask.Wait(wantedDelay, token);
                    return;
                }

                var remainingWaitTime = 0;
                while (!token.IsCancellationRequested)
                {
                    remainingWaitTime = wantedDelay - Convert.ToInt32(ElapsedWait.ElapsedMilliseconds);

                    if (remainingWaitTime >= 5)
                    {
                        Thread.Sleep(5);
                    }
                    else
                    {
                        try { delayTask.Wait(token); }
                        catch { /* ignore */ }
                    }

                    if (ElapsedWait.ElapsedMilliseconds >= wantedDelay)
                        break;
                }
            }
        }
    }
}
