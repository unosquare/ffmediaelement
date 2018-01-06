namespace Unosquare.FFME.Platform
{
    using System;
    using System.Runtime.InteropServices;
    using Shared;

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
            // Placeholder;
        }

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static WindowsNativeMethods Instance { get; }

        /// <summary>
        /// Fast pointer memory block copy function
        /// </summary>
        /// <param name="targetAddress">The target address.</param>
        /// <param name="sourceAddress">The source address.</param>
        /// <param name="copyLength">Length of the copy.</param>
        public void CopyMemory(IntPtr targetAddress, IntPtr sourceAddress, uint copyLength)
        {
            NativeMethods.CopyMemory(targetAddress, sourceAddress, copyLength);
        }

        /// <summary>
        /// Fills the memory with the specified value repeated.
        /// </summary>
        /// <param name="startAddress">The start address.</param>
        /// <param name="length">The length.</param>
        /// <param name="value">The value.</param>
        public void FillMemory(IntPtr startAddress, uint length, byte value)
        {
            NativeMethods.FillMemory(startAddress, length, value);
        }

        /// <summary>
        /// Sets the DLL directory in which external dependencies can be located.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>
        /// True for success. False for failure
        /// </returns>
        public bool SetDllDirectory(string path)
        {
            return NativeMethods.SetDllDirectory(path);
        }

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
            [DllImport(Kernel32, EntryPoint = "CopyMemory", SetLastError = false)]
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
