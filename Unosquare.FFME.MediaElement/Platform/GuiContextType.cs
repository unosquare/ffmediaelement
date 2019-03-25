namespace Unosquare.FFME.Platform
{
    /// <summary>
    /// Enumerates GUI Context Types
    /// </summary>
    public enum GuiContextType
    {
        /// <summary>
        /// An invalid GUI context (console applications)
        /// </summary>
        None,

#if WINDOWS_UWP
        /// <summary>
        /// A Universal Windows Platform GUI Context
        /// </summary>
        UWP,
#else
        /// <summary>
        /// A WPF GUI context (i.e. has dispatcher and is not Windows Forms)
        /// </summary>
        WPF,

        /// <summary>
        /// A Windows Forms GUI Context
        /// </summary>
        WinForms,
#endif
    }
}
