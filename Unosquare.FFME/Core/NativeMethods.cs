namespace Unosquare.FFME.Core
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// FFmpeg Registration Native Methods
    /// </summary>
    internal static class NativeMethods
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
