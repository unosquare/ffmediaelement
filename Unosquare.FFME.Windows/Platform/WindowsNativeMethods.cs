namespace Unosquare.FFME.Platform
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
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
        /// Enumerates memory copy methods
        /// </summary>
        private enum MemoryCopyStartegy
        {
            /// <summary>
            /// The native
            /// </summary>
            Native,

            /// <summary>
            /// The parallel native
            /// </summary>
            ParallelNative,

            /// <summary>
            /// The buffer
            /// </summary>
            Buffer
        }

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static WindowsNativeMethods Instance { get; }

        /// <summary>
        /// Gets or sets a value indicating whether Parallel Copy is enabled.
        /// </summary>
        private MemoryCopyStartegy CopyStrategy { get; set; } = MemoryCopyStartegy.Native;

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
        /// Fast pointer memory block copy function
        /// </summary>
        /// <param name="targetAddress">The target address.</param>
        /// <param name="sourceAddress">The source address.</param>
        /// <param name="copyLength">Length of the copy.</param>
        public unsafe void CopyMemory(IntPtr targetAddress, IntPtr sourceAddress, uint copyLength)
        {
            switch (CopyStrategy)
            {
                case MemoryCopyStartegy.Native:
                    {
                        NativeMethods.CopyMemory(targetAddress, sourceAddress, copyLength);
                        break;
                    }

                case MemoryCopyStartegy.ParallelNative:
                    {
                        CopyMemoryParallel(targetAddress, sourceAddress, copyLength);
                        break;
                    }

                case MemoryCopyStartegy.Buffer:
                    {
                        Buffer.MemoryCopy(sourceAddress.ToPointer(), targetAddress.ToPointer(), copyLength, copyLength);
                        break;
                    }
            }
        }

        /// <summary>
        /// An experimetal method of copying large chunks of memory in parallel.
        /// Does not seem to have any advantages of the native CopyMemory direct call.
        /// </summary>
        /// <param name="targetAddress">The target address.</param>
        /// <param name="sourceAddress">The source address.</param>
        /// <param name="copyLength">Length of the copy.</param>
        private unsafe void CopyMemoryParallel(IntPtr targetAddress, IntPtr sourceAddress, uint copyLength)
        {
            const int optimalBlockSize = 2048;
            if (copyLength <= optimalBlockSize)
            {
                NativeMethods.CopyMemory(targetAddress, sourceAddress, copyLength);
                return;
            }

            var chunkSize = (int)copyLength / 4; // optimalBlockSize;
            var blockCount = (int)(copyLength / chunkSize);

            Parallel.For(0, blockCount, (blockIndex) =>
            {
                var offset = blockIndex * chunkSize;
                NativeMethods.CopyMemory(targetAddress + offset, sourceAddress + offset, (uint)chunkSize);
            });

            var lastOffset = blockCount * chunkSize;
            if (lastOffset < copyLength)
            {
                NativeMethods.CopyMemory(targetAddress + lastOffset, sourceAddress + lastOffset, copyLength - (uint)lastOffset);
            }
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
