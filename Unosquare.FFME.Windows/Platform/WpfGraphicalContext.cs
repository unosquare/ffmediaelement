namespace Unosquare.FFME.Platform
{
    using Shared;
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Threading;

    /// <summary>
    /// The WPF graphical context
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Platform.IGuiContext" />
    internal class WpfGraphicalContext : IGuiContext
    {
        /// <summary>
        /// The WPF dispatcher
        /// </summary>
        private static Dispatcher WpfDispatcher = null;

        /// <summary>
        /// Initializes static members of the <see cref="WpfGraphicalContext"/> class.
        /// </summary>
        static WpfGraphicalContext()
        {
            Current = new WpfGraphicalContext();
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="WpfGraphicalContext"/> class from being created.
        /// </summary>
        private WpfGraphicalContext()
        {
            WpfDispatcher = WpfDispatcher = Application.Current?.Dispatcher;
            IsValid = WpfDispatcher != null && WpfDispatcher is System.Windows.Threading.Dispatcher;
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
        public static WpfGraphicalContext Current { get; }

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
        public void EnqueueInvoke(ActionPriority priority, Delegate callback, params object[] arguments)
        {
            WpfDispatcher.BeginInvoke(callback, (DispatcherPriority)priority, arguments);
        }

        /// <summary>
        /// Synchronously invokes the call on the UI thread
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        public void Invoke(ActionPriority priority, Action action)
        {
            WpfDispatcher.Invoke(action, (DispatcherPriority)priority, null);
        }
    }
}
