namespace Unosquare.FFME.Platform
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Windows;
    using Unosquare.FFME.Shared;

    /// <summary>
    /// The Windows forms graphical context
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Platform.IGuiContext" />
    internal class WinFormsGraphicalContext : IGuiContext
    {
        /// <summary>
        /// The application synchronization context
        /// </summary>
        private SynchronizationContext WinFormsContext = null;

        /// <summary>
        /// Initializes static members of the <see cref="WinFormsGraphicalContext"/> class.
        /// </summary>
        static WinFormsGraphicalContext()
        {
            Current = new WinFormsGraphicalContext();
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="WinFormsGraphicalContext"/> class from being created.
        /// </summary>
        private WinFormsGraphicalContext()
        {
            WinFormsContext = SynchronizationContext.Current;
            IsValid = WinFormsContext != null && WinFormsContext is System.Windows.Forms.WindowsFormsSynchronizationContext;
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
        /// Gets the current.
        /// </summary>
        public static WinFormsGraphicalContext Current { get; }

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
            var postState = new Tuple<Delegate, object[]>(callback, arguments);
            WinFormsContext.Post((s) =>
            {
                var a = s as Tuple<Delegate, object[]>;
                a.Item1.DynamicInvoke(a.Item2);
            }, postState);
            return;
        }

        /// <summary>
        /// Synchronously invokes the call on the UI thread
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        public void Invoke(ActionPriority priority, Action action)
        {
            WinFormsContext.Send((s) => { action(); }, priority);
        }
    }
}
