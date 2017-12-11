namespace Unosquare.FFME.Windows.Sample
{
    using System;
    using System.Runtime.InteropServices;
    using System.Security;

    /// <summary>
    /// Represents a Console Manager static object
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    public static class ConsoleManager
    {
        /// <summary>
        /// The sw hide
        /// </summary>
        public const int SW_HIDE = 0;

        /// <summary>
        /// The sw show
        /// </summary>
        public const int SW_SHOW = 5;
        
        /// <summary>
        /// Allocs the console.
        /// </summary>
        /// <returns><c>true</c> if the console was allocated</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AllocConsole(); // Create console window

        /// <summary>
        /// Gets the console window.
        /// </summary>
        /// <returns>The pointer to the console window</returns>
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow(); // Get console window handle

        /// <summary>
        /// Shows the window.
        /// </summary>
        /// <param name="hWnd">The h WND.</param>
        /// <param name="nCmdShow">The n command show.</param>
        /// <returns><c>true</c> if the window is shown</returns>
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Shows the console.
        /// </summary>
        public static void ShowConsole()
        {
            var handle = GetConsoleWindow();
            if (handle == IntPtr.Zero)
                AllocConsole();
            else
                ShowWindow(handle, SW_SHOW);
        }

        /// <summary>
        /// Hides the console.
        /// </summary>
        public static void HideConsole()
        {
            var handle = GetConsoleWindow();

            if (handle != null)
                ShowWindow(handle, SW_HIDE);
        }
    }
}
