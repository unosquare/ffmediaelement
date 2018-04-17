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
            catch { }

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
        /// Gets a value indicating whetherthe context is in design time
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
        /// <param name="arguments">The arguments.</param>
        /// <returns>The awaitable task.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task InvokeAsync(DispatcherPriority priority, Delegate callback, params object[] arguments)
        {
            if (Thread == Thread.CurrentThread)
            {
                callback.DynamicInvoke(arguments);
                return Task.CompletedTask;
            }

            switch (Type)
            {
                case GuiContextType.None:
                    {
                        return Task.Run(() => { callback.DynamicInvoke(arguments); });
                    }

                case GuiContextType.WPF:
                    {
                        return GuiDispatcher.InvokeAsync(() => { callback.DynamicInvoke(arguments); }, priority).Task;
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

                        return Task.Run(() =>
                        {
                            doneEvent.Wait();
                            doneEvent.Dispose();
                        });
                    }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Invokes a task on the GUI thread
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="callback">The callback.</param>
        /// <returns>The awaitable task</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task InvokeAsync(DispatcherPriority priority, Action callback)
        {
            return InvokeAsync(priority, callback, null);
        }

        /// <summary>
        /// Invokes a task on the GUI thread
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <returns>The awaitable task</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task InvokeAsync(Action callback)
        {
            return InvokeAsync(DispatcherPriority.DataBind, callback, null);
        }

        /// <summary>
        /// Invokes a task on the GUI thread
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <returns>The awaitable task</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task EnqueueInvoke(Action callback)
        {
            return InvokeAsync(callback);
        }

        /// <summary>
        /// Invokes a task on the GUI thread
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="callback">The callback.</param>
        /// <returns>The awaitable task</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task EnqueueInvoke(DispatcherPriority priority, Action callback)
        {
            return InvokeAsync(priority, callback);
        }
    }
}
