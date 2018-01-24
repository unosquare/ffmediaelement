namespace Unosquare.FFME.Platform
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Threading;

    /// <summary>
    /// The WPF graphical context
    /// </summary>
    /// <seealso cref="IGuiContext" />
    internal class WpfGuiContext : IGuiContext
    {
        private static readonly object SyncLock = new object();

        /// <summary>
        /// The WPF dispatcher
        /// </summary>
        private static Dispatcher GuiDispatcher = null;

        /// <summary>
        /// Initializes static members of the <see cref="WpfGuiContext"/> class.
        /// </summary>
        static WpfGuiContext()
        {
            Current = new WpfGuiContext();
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="WpfGuiContext"/> class from being created.
        /// </summary>
        private WpfGuiContext()
        {
            GuiDispatcher = Application.Current?.Dispatcher;
            IsValid = GuiDispatcher != null && GuiDispatcher is Dispatcher;

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
        public static WpfGuiContext Current { get; }

        /// <summary>
        /// Gets a value indicating whetherthe context is in design time
        /// </summary>
        public bool IsInDesignTime { get; }

        /// <summary>
        /// Returns true if this context is valid.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Enqueues a UI call
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="callback">The callback.</param>
        /// <param name="arguments">The arguments.</param>
        public void EnqueueInvoke(DispatcherPriority priority, Delegate callback, params object[] arguments)
        {
            GuiDispatcher.BeginInvoke(callback, priority, arguments);
        }

        /// <summary>
        /// Synchronously invokes the call on the UI thread
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        public void Invoke(DispatcherPriority priority, Action action)
        {
            lock (SyncLock)
            {
                if (Dispatcher.CurrentDispatcher?.Thread.ManagedThreadId == GuiDispatcher.Thread.ManagedThreadId)
                    action();
                else
                    GuiDispatcher.Invoke(action, priority, null);
            }
        }
    }
}
