namespace Unosquare.FFME.Platform
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Provides platform-specific methods sensible to a UI context.
    /// </summary>
    internal interface IGuiContext
    {
        /// <summary>
        /// Invokes a task on the GUI thread with the possibility of awaiting it.
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <returns>The awaitable task.</returns>
        ConfiguredTaskAwaitable InvokeAsync(Action callback);

        /// <summary>
        /// Invokes a task on the GUI thread and does not await it.
        /// </summary>
        /// <param name="callback">The callback.</param>
        void EnqueueInvoke(Action callback);
    }
}
