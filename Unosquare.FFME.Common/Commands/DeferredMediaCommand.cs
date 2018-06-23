namespace Unosquare.FFME.Commands
{
    using Primitives;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class DeferredMediaCommand
    {
        private readonly IWaitEvent WaitEvent = WaitEventFactory.Create(isCompleted: false, useSlim: true);

        protected DeferredMediaCommand()
        {
            // TODO: constructor
        }

        public async Task RunAsync()
        {
            WaitEvent.Begin();

            ThreadPool.QueueUserWorkItem((s) =>
            {
                try { ExecuteInternal(); }
                catch (Exception ex) { HandleException(ex); }
                finally { WaitEvent.Complete(); }
            });

            await Task.Run(() =>
            {
                WaitEvent.Wait();
                WaitEvent.Dispose();
            });
        }

        protected abstract void ExecuteInternal();

        protected abstract void HandleException(Exception ex);
    }
}
