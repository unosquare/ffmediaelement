namespace Unosquare.FFME.Platform
{
    using Shared;
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Windows-specific native methods
    /// </summary>
    internal class WindowsNativeMethods : INativeMethods
    {
        /// <summary>
        /// Initializes static members of the <see cref="WindowsNativeMethods"/> class.
        /// </summary>
        static WindowsNativeMethods()
        {
            Instance = new WindowsNativeMethods();
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="WindowsNativeMethods"/> class from being created.
        /// </summary>
        private WindowsNativeMethods()
        {
            // placeholder
        }

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static WindowsNativeMethods Instance { get; }

        /// <inheritdoc />
        public void FillMemory(IntPtr startAddress, uint length, byte value) =>
            NativeMethods.FillMemory(startAddress, length, value);
        
        /// <inheritdoc />
        public bool SetDllDirectory(string path) =>
            NativeMethods.SetDllDirectory(path);

        /// <inheritdoc />
        public void CopyMemory(IntPtr targetAddress, IntPtr sourceAddress, uint copyLength) =>
            NativeMethods.CopyMemory(targetAddress, sourceAddress, copyLength);

        /// <summary>
        /// Contains Interop native methods
        /// </summary>
        private static class NativeMethods
        {
            private const string Kernel32 = "kernel32.dll";

            /// <summary>
            /// Sets the DLL directory in which external dependencies can be located.
            /// </summary>
            /// <param name="lpPathName">the full path.</param>
            /// <returns>True if set, false if not set</returns>
            [DllImport(Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool SetDllDirectory(string lpPathName);

            /// <summary>
            /// Fast pointer memory block copy function
            /// </summary>
            /// <param name="destination">The destination.</param>
            /// <param name="source">The source.</param>
            /// <param name="length">The length.</param>
            [DllImport(Kernel32, EntryPoint = "RtlCopyMemory", SetLastError = false)]
            public static extern void CopyMemory(IntPtr destination, IntPtr source, uint length);

            /// <summary>
            /// Fills the memory.
            /// </summary>
            /// <param name="destination">The destination.</param>
            /// <param name="length">The length.</param>
            /// <param name="fill">The fill.</param>
            [DllImport(Kernel32, EntryPoint = "RtlFillMemory", SetLastError = false)]
            public static extern void FillMemory(IntPtr destination, uint length, byte fill);
        }
    }
}
