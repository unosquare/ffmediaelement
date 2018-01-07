namespace Unosquare.FFME.Platform
{
    using Shared;
    using System;

    /// <summary>
    /// Defines a generic graphical context (compatibility between WPF and WinForms apps)
    /// </summary>
    internal interface IGuiContext
    {
        /// <summary>
        /// Gets a value indicating whetherthe context is in design time
        /// </summary>
        bool IsInDesignTime { get; }

        /// <summary>
        /// Returns true if the graphical context is valid.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Enqueues a UI call
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="callback">The callback.</param>
        /// <param name="arguments">The arguments.</param>
        void EnqueueInvoke(ActionPriority priority, Delegate callback, params object[] arguments);

        /// <summary>
        /// Synchronously invokes the call on the UI thread
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        void Invoke(ActionPriority priority, Action action);
    }
}
