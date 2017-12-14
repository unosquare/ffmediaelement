namespace Unosquare.FFME.Core
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;

    /// <summary>
    /// Provides helpers tor un code in different modes on the UI dispatcher.
    /// </summary>
    internal static class Runner
    {
        /// <summary>
        /// Gets the UI dispatcher.
        /// </summary>
        public static Dispatcher UIDispatcher => Application.Current?.Dispatcher;

        /// <summary>
        /// Synchronously invokes the given instructions on the main application dispatcher.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        public static void UIInvoke(DispatcherPriority priority, Action action)
        {
            UIDispatcher?.Invoke(action, priority, null);
        }

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main application dispatcher.
        /// This is a way to execute code in a fire-and-forget style
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task UIEnqueueInvoke(DispatcherPriority priority, Delegate action, params object[] args)
        {
            try
            {
                // Call the code on the UI dispatcher
                await UIDispatcher?.BeginInvoke(action, priority, args);
            }
            catch (TaskCanceledException)
            {
                // Swallow task cancellation exceptions. This is ok.
                return;
            }
            catch
            {
                // Retrhow the exception
                // TODO: Maybe logging here would be helpful?
                throw;
            }
        }

        /// <summary>
        /// Creates an empty pump operation with background priority
        /// </summary>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <returns>an empty pump operation</returns>
        public static DispatcherOperation CreatePumpOperation(this Dispatcher dispatcher)
        {
            return dispatcher.BeginInvoke(
                DispatcherPriority.Background, new Action(async () => { await Dispatcher.Yield(); }));
        }

        /// <summary>
        /// Creates the asynchronous waiter.
        /// </summary>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <param name="backgroundTask">The background task.</param>
        /// <returns>
        /// a dsipatcher operation that can be awaited
        /// </returns>
        public static DispatcherOperation CreateAsynchronousPumpWaiter(this Dispatcher dispatcher, Task backgroundTask)
        {
            var operation = dispatcher.BeginInvoke(new Action(() =>
            {
                var pumpTimes = 0;
                while (backgroundTask.IsCompleted == false)
                {
                    // Pump invoke
                    pumpTimes++;
                    Dispatcher.CurrentDispatcher.Invoke(
                        DispatcherPriority.Background,
                        new Action(async () => { await Dispatcher.Yield(); }));
                }

                System.Diagnostics.Debug.WriteLine($"{nameof(CreateAsynchronousPumpWaiter)}: Pump Times: {pumpTimes}");
            }), DispatcherPriority.Input);

            return operation;
        }

        /// <summary>
        /// Exits the execution frame.
        /// </summary>
        /// <param name="f">The f.</param>
        /// <returns>Always a null value</returns>
        private static object ExitFrame(object f)
        {
            (f as DispatcherFrame).Continue = false;
            return null;
        }
    }
}
