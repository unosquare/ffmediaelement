namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a class that implements delay logic for thread workers
    /// </summary>
    internal static class WorkerDelayProvider
    {
        /// <summary>
        /// Gets the default delay provider.
        /// </summary>
        public static IWorkerDelayProvider Default => TokenTimeout;

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
        /// Provides a delay implementation which uses short delay intervals of 5ms and
        /// a wait on the delay task in the final loop.
        /// </summary>
        public static IWorkerDelayProvider SteppedToken => new SteppedTokenDelay();

        private class TokenCancellableDelay : IWorkerDelayProvider
        {
            public void ExecuteCycleDelay(int wantedDelay, Task delayTask, CancellationToken token)
            {
                if (wantedDelay == 0 || wantedDelay < -1)
                    return;

                // for wanted delays of less than 30ms it is not worth
                // passing a timeout or a token as it only adds unnecessary
                // overhead.
                if (wantedDelay <= 30)
                {
                    try { delayTask.Wait(); }
                    catch { /* ignore */ }
                    return;
                }

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

                // for wanted delays of less than 30ms it is not worth
                // passing a timeout or a token as it only adds unnecessary
                // overhead.
                if (wantedDelay <= 30)
                {
                    try { delayTask.Wait(); }
                    catch { /* ignore */ }
                    return;
                }

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

        private class SteppedTokenDelay : IWorkerDelayProvider
        {
            private const int StepMilliseconds = 15;
            private readonly Stopwatch ElapsedWait = new Stopwatch();

            public void ExecuteCycleDelay(int wantedDelay, Task delayTask, CancellationToken token)
            {
                ElapsedWait.Restart();

                if (wantedDelay == 0 || wantedDelay < -1)
                    return;

                if (wantedDelay == Timeout.Infinite)
                {
                    try { delayTask.Wait(wantedDelay, token); }
                    catch { /* Ignore cancelled tasks */ }
                    return;
                }

                while (!token.IsCancellationRequested)
                {
                    var remainingWaitTime = wantedDelay - Convert.ToInt32(ElapsedWait.ElapsedMilliseconds);

                    // Exit for no remaining wait time
                    if (remainingWaitTime <= 0)
                        break;

                    if (remainingWaitTime >= StepMilliseconds)
                    {
                        Task.Delay(StepMilliseconds).Wait();
                    }
                    else
                    {
                        try { delayTask.Wait(remainingWaitTime); }
                        catch { /* ignore cancellation of task exception */ }
                    }

                    if (ElapsedWait.ElapsedMilliseconds >= wantedDelay)
                        break;
                }
            }
        }
    }
}
