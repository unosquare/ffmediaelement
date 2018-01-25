namespace Unosquare.FFME.Platform
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Threading;

    /// <summary>
    /// The WPF or WinForms graphical context
    /// </summary>
    internal class GuiContext
    {
        private SynchronizationContext Context = null;
        private Dispatcher GuiDispatcher = null;

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
            Context = SynchronizationContext.Current;
            ContextType = GuiContextType.None;
            if (Context is DispatcherSynchronizationContext) ContextType = GuiContextType.WPF;
            else if (Context is WindowsFormsSynchronizationContext) ContextType = GuiContextType.WinForms;

            IsValid = Context != null && ContextType != GuiContextType.None;

            if (ContextType == GuiContextType.WPF)
                GuiDispatcher = System.Windows.Application.Current.Dispatcher;

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
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static GuiContext Current { get; }

        /// <summary>
        /// Gets a value indicating whetherthe context is in design time
        /// </summary>
        public bool IsInDesignTime { get; }

        /// <summary>
        /// Returns true if this context is valid.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the type of the context.
        /// </summary>
        public GuiContextType ContextType { get; }

        public async Task InvokeAsync(DispatcherPriority priority, Delegate callback, params object[] arguments)
        {
            if (Context != null && Context == SynchronizationContext.Current)
            {
                callback.DynamicInvoke(arguments);
                return;
            }

            switch (ContextType)
            {
                case GuiContextType.None:
                    {
                        await Task.Run(() => { callback.DynamicInvoke(arguments); });
                        return;
                    }

                case GuiContextType.WPF:
                    {
                        await GuiDispatcher.InvokeAsync(() => { callback.DynamicInvoke(arguments); }, priority);
                        return;
                    }

                case GuiContextType.WinForms:
                    {
                        // TODO: Testing required
                        var doneEvent = new ManualResetEventSlim(false);
                        Context.Post((args) =>
                        {
                            try
                            {
                                callback.DynamicInvoke(args);
                            }
                            catch { throw; }
                            finally { doneEvent.Set(); }
                        }, arguments);

                        await Task.Run(() =>
                        {
                            doneEvent.Wait();
                            doneEvent.Dispose();
                        });

                        return;
                    }
            }
        }

        public async Task InvokeAsync(DispatcherPriority priority, Action callback)
        {
            await InvokeAsync(priority, callback, null);
        }

        public async Task InvokeAsync(Action callback)
        {
            await InvokeAsync(DispatcherPriority.DataBind, callback, null);
        }

        public void Invoke(Action callback)
        {
            InvokeAsync(DispatcherPriority.Normal, callback).GetAwaiter().GetResult();
        }

        public Task EnqueueInvoke(Action callback)
        {
            return InvokeAsync(callback);
        }

        public Task EnqueueInvoke(DispatcherPriority priority, Action callback)
        {
            return InvokeAsync(priority, callback);
        }
    }
}
