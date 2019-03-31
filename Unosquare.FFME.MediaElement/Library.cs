namespace Unosquare.FFME
{
    using System;
    using Platform;

    public static partial class Library
    {
        private static IGuiContext m_GuiContext;

        /// <summary>
        /// Provides access to the registered GUI context
        /// </summary>
        internal static IGuiContext GuiContext
        {
            get
            {
                lock (SyncLock)
                {
                    if (m_GuiContext == null)
                        throw new InvalidOperationException($"{nameof(IGuiContext)} has not been registered.");

                    return m_GuiContext;
                }
            }
        }

        /// <summary>
        /// Registers the GUI context for the library.
        /// </summary>
        /// <param name="context">The GUI context to register.</param>
        internal static void RegisterGuiContext(IGuiContext context)
        {
            lock (SyncLock)
            {
                if (m_GuiContext != null)
                    throw new InvalidOperationException($"{nameof(GuiContext)} has already been registered.");

                m_GuiContext = context ?? throw new ArgumentNullException($"{nameof(context)} cannot be null.");
            }
        }
    }
}
