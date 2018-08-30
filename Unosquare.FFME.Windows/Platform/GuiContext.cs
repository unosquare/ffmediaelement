namespace Unosquare.FFME.Platform
{
    using Primitives;
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Threading;

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
            try { GuiDispatcher = System.Windows.Application.Current.Dispatcher; }
            catch { /* Ignore error as app might not be available or context is not WPF */ }

            Type = GuiContextType.None;
            if (GuiDispatcher != null) Type = GuiContextType.WPF;
            else if (ThreadContext is WindowsFormsSynchronizationContext) Type = GuiContextType.WinForms;

            IsValid = Type != GuiContextType.None;

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
        /// Invokes a task on the GUI thread
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="callback">The callback.</param>
        /// <returns>The awaitable task</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task InvokeAsync(DispatcherPriority priority, Action callback) =>
            await InvokeAsyncInternal(priority, callback, null).ConfigureAwait(false);

        /// <summary>
        /// Invokes a task on the GUI thread
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <returns>The awaitable task</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task InvokeAsync(Action callback) =>
            await InvokeAsyncInternal(DispatcherPriority.DataBind, callback, null).ConfigureAwait(false);

        /// <summary>
        /// Invokes a task on the GUI thread
        /// </summary>
        /// <param name="callback">The callback.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnqueueInvoke(Action callback)
        {
#pragma warning disable 4014
            InvokeAsync(callback).ConfigureAwait(false);
#pragma warning restore 4014
        }

        /// <summary>
        /// Invokes a task on the GUI thread
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="callback">The callback.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnqueueInvoke(DispatcherPriority priority, Action callback)
        {
#pragma warning disable 4014
            InvokeAsync(priority, callback).ConfigureAwait(false);
#pragma warning restore 4014
        }

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

            switch (Type)
            {
                case GuiContextType.WPF:
                    {
                        await GuiDispatcher.InvokeAsync(() => { callback.DynamicInvoke(arguments); }, priority);
                        return;
                    }

                case GuiContextType.WinForms:
                    {
                        var doneEvent = WaitEventFactory.Create(isCompleted: false, useSlim: true);
                        ThreadContext.Post((args) =>
                        {
                            try
                            {
                                callback.DynamicInvoke(args as object[]);
                            }
                            catch { throw; }
                            finally { doneEvent.Complete(); }
                        }, arguments);

                        var waitingTask = new Task(() =>
                        {
                            doneEvent.Wait();
                            doneEvent.Dispose();
                        });

                        var awaiter = waitingTask.ConfigureAwait(false);
                        waitingTask.Start();
                        await awaiter;

                        return;
                    }

                case GuiContextType.None:
                default:
                    {
                        var runnerTask = new Task(() => { callback.DynamicInvoke(arguments); });
                        var awaiter = runnerTask.ConfigureAwait(false);
                        runnerTask.Start();

                        await awaiter;
                        return;
                    }
            }
        }
    }
}
