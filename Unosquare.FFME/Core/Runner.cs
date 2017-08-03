namespace Unosquare.FFME.Core
{
    using System;
    using System.Security.Permissions;
    using System.Threading;
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
        public static Dispatcher UIDispatcher
        {
            get { return Application.Current?.Dispatcher; }
        }

        /// <summary>
        /// Synchronously invokes the given instructions on the main application dispatcher.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        public static void UIInvoke(DispatcherPriority priority, Action action)
        {
            UIDispatcher.Invoke(action, priority, null);
        }

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main application dispatcher.
        /// This is a way to execute code in a fire-and-forget style
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        /// <param name="args">The arguments.</param>
        public static void UIEnqueueInvoke(DispatcherPriority priority, Delegate action, params object[] args)
        {
            UIDispatcher.BeginInvoke(action, priority, args);
        }

        /// <summary>
        /// Starts the action in a background thread while it continues to pump UI executions.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        public static void UIPumpInvoke(DispatcherPriority priority, Action action)
        {
            var completer = new ManualResetEvent(false);

            try
            {
                ThreadPool.QueueUserWorkItem((o) =>
                {
                    action();
                    completer.Set();
                });

                while (completer.WaitOne(1) == false)
                    DoEvents();
            }
            catch
            {
                // placeholder
            }
            finally
            {
                completer.Dispose();
            }
        }

        /// <summary>
        /// Pumps frames into the UI dispatcher queue that enable
        /// the continued execution of delegates.
        /// </summary>
        [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        public static void DoEvents()
        {
            var frame = new DispatcherFrame();
            UIDispatcher?.BeginInvoke(
                DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
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
