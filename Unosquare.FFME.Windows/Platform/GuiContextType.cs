﻿namespace Unosquare.FFME.Platform
{
    /// <summary>
    /// Enumerates GUI Context Types.
    /// </summary>
    internal enum GuiContextType
    {
        /// <summary>
        /// An invalid GUI context (console applications)
        /// </summary>
        None,

        /// <summary>
        /// A WPF GUI context (i.e. has dispatcher and is not Windows Forms)
        /// </summary>
        WPF,

        /// <summary>
        /// A Windows Forms GUI Context
        /// </summary>
        WinForms
    }
}
