namespace Unosquare.FFME.Platform
{
    using Shared;
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    /// <summary>
    /// Windows-specific native methods
    /// </summary>
    internal class WindowsNativeMethods : INativeMethods
    {
        private static readonly int Parallelism;

        /// <summary>
        /// Initializes static members of the <see cref="WindowsNativeMethods"/> class.
        /// </summary>
        static WindowsNativeMethods()
        {
            Parallelism = (int)Math.Max(1, Environment.ProcessorCount * 0.8);
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
        /// Enumerates memory copy methods
        /// </summary>
        private enum MemoryCopyStrategy
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
        private MemoryCopyStrategy CopyStrategy { get; } = MemoryCopyStrategy.ParallelNative;

        /// <inheritdoc />
        public void FillMemory(IntPtr startAddress, uint length, byte value) =>
            NativeMethods.FillMemory(startAddress, length, value);

        /// <summary>
        /// Zeroes the memory.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="length">The length.</param>
        public void ZeroMemory(IntPtr destination, int length) =>
            NativeMethods.ZeroMemory(destination, length);

        /// <inheritdoc />
        public bool SetDllDirectory(string path) =>
            NativeMethods.SetDllDirectory(path);

        /// <inheritdoc />
        public unsafe void CopyMemory(IntPtr targetAddress, IntPtr sourceAddress, uint copyLength)
        {
            switch (CopyStrategy)
            {
                case MemoryCopyStrategy.Native:
                    {
                        NativeMethods.CopyMemory(targetAddress, sourceAddress, copyLength);
                        break;
                    }

                case MemoryCopyStrategy.ParallelNative:
                    {
                        CopyMemoryParallel(targetAddress, sourceAddress, copyLength);
                        break;
                    }

                default:
                    {
                        Buffer.MemoryCopy((void*)sourceAddress, (void*)targetAddress, copyLength, copyLength);
                        break;
                    }
            }
        }

        /// <summary>
        /// An experimental method of copying large chunks of memory in parallel.
        /// Does not seem to have any advantages of the native CopyMemory direct call.
        /// </summary>
        /// <param name="targetAddress">The target address.</param>
        /// <param name="sourceAddress">The source address.</param>
        /// <param name="copyLength">Length of the copy.</param>
        private void CopyMemoryParallel(IntPtr targetAddress, IntPtr sourceAddress, uint copyLength)
        {
            const int optimalBlockSize = 1024 * 1024 * 2; // 2MB per thread
            const int maxParallelism = 4;

            // Don't run parallelism for smaller chunks -- it's not worth it
            if (copyLength <= optimalBlockSize)
            {
                NativeMethods.CopyMemory(targetAddress, sourceAddress, copyLength);
                return;
            }

            var blockCount = (int)Math.Max(1, Math.Min(Parallelism, copyLength / optimalBlockSize));
            if (blockCount > maxParallelism) blockCount = maxParallelism;
            var lastBlockIndex = blockCount - 1;
            var blockSize = Convert.ToUInt32(copyLength / blockCount);
            var lastBlockSize = blockSize + (copyLength % blockSize);

            // No need to run in parallel if we have only 1 block.
            if (blockCount <= 1)
            {
                NativeMethods.CopyMemory(targetAddress, sourceAddress, copyLength);
                return;
            }

            // Start the copy operation in the thread pool
            Parallel.For(0, blockCount, blockIndex =>
            {
                var offset = blockIndex * (int)blockSize;
                var chunkSize = blockIndex == lastBlockIndex ? lastBlockSize : blockSize;
                NativeMethods.CopyMemory(targetAddress + offset, sourceAddress + offset, chunkSize);
            });
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

            /// <summary>
            /// Zeroes the memory.
            /// </summary>
            /// <param name="dest">The dest.</param>
            /// <param name="length">The length.</param>
            [DllImport(Kernel32, EntryPoint = "RtlZeroMemory", SetLastError = false)]
            public static extern void ZeroMemory(IntPtr dest, int length);
        }
    }
}
