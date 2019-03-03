namespace Unosquare.FFME.MacOS.Platform
{
    using System;
    using FFmpeg.AutoGen;
    using Unosquare.FFME.Shared;

    public class MacNativeMethods : INativeMethods
    {
        /// <summary>
        /// Initializes static members of the <see cref="MacNativeMethods"/> class.
        /// </summary>
        static MacNativeMethods()
        {
            Instance = new MacNativeMethods();
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="MacNativeMethods"/> class from being created.
        /// </summary>
        private MacNativeMethods()
        {
            // placeholder
        }

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static MacNativeMethods Instance { get; }
		
        public bool SetDllDirectory(string lpPathName)
        {
            if (lpPathName != null)
                ffmpeg.RootPath = lpPathName ?? string.Empty;
            return true;
        }

        /// <inheritdoc />
        public void FillMemory(IntPtr startAddress, uint length, byte value) =>
            NativeMethods.memset(startAddress, value, length);

        /// <inheritdoc />
        public void CopyMemory(IntPtr targetAddress, IntPtr sourceAddress, uint copyLength) =>
            NativeMethods.memcpy(targetAddress, sourceAddress, copyLength);

        /// <summary>
        /// Contains Interop native methods
        /// </summary>
        private static class NativeMethods
        {
            private const string Libc = "libc";

            /// <summary>
            /// Fast pointer memory block copy function
            /// </summary>
            /// <param name="destination">The destination.</param>
            /// <param name="source">The source.</param>
            /// <param name="length">The length.</param>
            [DllImport(Libc, EntryPoint = "memcpy", SetLastError = false)]
            public static extern IntPtr memcpy(IntPtr destination, IntPtr source, uint length);

            /// <summary>
            /// Fills the memory.
            /// </summary>
            /// <param name="destination">The destination.</param>
            /// <param name="fill">The fill.</param>
            /// <param name="length">The length.</param>
            [DllImport(Libc, EntryPoint = "memset", SetLastError = false)]
            public static extern void memset(IntPtr destination, byte fill, uint length);
        }
    }
}
