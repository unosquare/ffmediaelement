namespace Unosquare.FFME.Platform
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

#if WINDOWS_UWP
    using Windows.ApplicationModel;
    using Application = Windows.ApplicationModel.Core.CoreApplication;
    using Dispatcher = Windows.UI.Core.CoreDispatcher;
    using DispatcherPriority = Windows.UI.Core.CoreDispatcherPriority;
#else
    using Primitives;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Threading;
    using Application = System.Windows.Application;
#endif

    /// <summary>
    /// Provides properties and methods for the
    /// WPF or Windows Forms GUI Threading context
    /// </summary>
    public sealed class GuiContext
    {
        /// <summary>
        /// Initializes static members of the <see cref="GuiContext"/> class.
        /// </summary>
        static GuiContext()
        {
            Current = new GuiContext();
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="GuiContext"/> class from being created.
        /// </summary>
        private GuiContext()
        {
            Thread = Thread.CurrentThread;
            ThreadContext = SynchronizationContext.Current;

#if WINDOWS_UWP
            try
            {
                GuiDispatcher = Application.MainView.Dispatcher;
                Type = GuiContextType.UWP;
            }
            catch
            {
                GuiDispatcher = null;
                Type = GuiContextType.None;
            }

            IsInDesignTime = DesignMode.DesignModeEnabled;
#else
            try { GuiDispatcher = Application.Current.Dispatcher; }
            catch { /* Ignore error as app might not be available or context is not WPF */ }

            Type = GuiContextType.None;
            if (GuiDispatcher != null) Type = GuiContextType.WPF;
            else if (ThreadContext is WindowsFormsSynchronizationContext) Type = GuiContextType.WinForms;

            // Design-time detection
            try
            {
                IsInDesignTime = (bool)DesignerProperties.IsInDesignModeProperty.GetMetadata(
                    typeof(DependencyObject)).DefaultValue;
            }
            catch
            {
                IsInDesignTime = false;
            }
#endif
            IsValid = Type != GuiContextType.None;
            IsInDebugMode = Debugger.IsAttached;
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static GuiContext Current { get; }

        /// <summary>
        /// Gets the type of the context.
        /// </summary>
        public GuiContextType Type { get; }

        /// <summary>
        /// Gets the thread on which this context was created
        /// </summary>
        public Thread Thread { get; }

        /// <summary>
        /// Gets a value indicating whether the context is in design time
        /// </summary>
        public bool IsInDesignTime { get; }

        /// <summary>
        /// Gets a value indicating whether a debugger was attached when the context initialized.
        /// </summary>
        public bool IsInDebugMode { get; }

        /// <summary>
        /// Returns true if this context is valid.
        /// </summary>
        internal bool IsValid { get; }

        /// <summary>
        /// Gets the synchronization context.
        /// </summary>
        internal SynchronizationContext ThreadContext { get; }

        /// <summary>
        /// Gets the GUI dispatcher. Only valid for WPF contexts
        /// </summary>
        internal Dispatcher GuiDispatcher { get; }

        /// <summary>
        /// Invokes a task on the GUI thread with the possibility of awaiting it.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="callback">The callback.</param>
        /// <returns>The awaitable task</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConfiguredTaskAwaitable InvokeAsync(DispatcherPriority priority, Action callback) =>
            InvokeAsyncInternal(priority, callback, null).ConfigureAwait(true);

        /// <summary>
        /// Invokes a task on the GUI thread with the possibility of awaiting it.
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <returns>The awaitable task</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConfiguredTaskAwaitable InvokeAsync(Action callback) =>
#if WINDOWS_UWP
            InvokeAsyncInternal(DispatcherPriority.Normal, callback, null).ConfigureAwait(true);
#else
            InvokeAsyncInternal(DispatcherPriority.DataBind, callback, null).ConfigureAwait(true);
#endif

        /// <summary>
        /// Invokes a task on the GUI thread and does not await it.
        /// </summary>
        /// <param name="callback">The callback.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnqueueInvoke(Action callback) => InvokeAsync(callback);

        /// <summary>
        /// Invokes a task on the GUI thread
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="callback">The callback.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnqueueInvoke(DispatcherPriority priority, Action callback) => InvokeAsync(priority, callback);

        /// <summary>
        /// Invokes a task on the GUI thread
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="callback">The callback.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The awaitable task.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task InvokeAsyncInternal(DispatcherPriority priority, Delegate callback, params object[] arguments)
        {
            if (Thread == Thread.CurrentThread)
            {
                callback.DynamicInvoke(arguments);
                return;
            }

            try
            {
                // We try here because we'd like to catch cancellations and ignore then
                switch (Type)
                {
#if WINDOWS_UWP
                    case GuiContextType.UWP:
                        {
                            await GuiDispatcher.RunAsync(priority, () => { callback.DynamicInvoke(arguments); });
                            return;
                        }
#else
                    case GuiContextType.WPF:
                        {
                            await GuiDispatcher.InvokeAsync(() => { callback.DynamicInvoke(arguments); }, priority);
                            return;
                        }

                    case GuiContextType.WinForms:
                        {
                            var doneEvent = WaitEventFactory.Create(isCompleted: false, useSlim: true);
                            ThreadContext.Post(a =>
                            {
                                try { callback.DynamicInvoke(arguments); }
                                finally { doneEvent.Complete(); }
                            }, null);

                            var waitingTask = new Task(() =>
                            {
                                doneEvent.Wait();
                                doneEvent.Dispose();
                            });

                            waitingTask.Start();
                            await waitingTask.ConfigureAwait(true);

                            return;
                        }
#endif

                    default:
                        {
                            var runnerTask = new Task(() => { callback.DynamicInvoke(arguments); });
                            runnerTask.Start();

                            await runnerTask.ConfigureAwait(true);
                            return;
                        }
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
                Debug.WriteLine($"FFME {nameof(GuiContext)}.{nameof(InvokeAsyncInternal)}: Operation was cancelled");
            }
        }
    }
}
