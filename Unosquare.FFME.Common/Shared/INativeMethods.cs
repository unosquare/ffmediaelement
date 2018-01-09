namespace Unosquare.FFME.Shared
{
    using System;

    /// <summary>
    /// Defines platform-specific methods
    /// </summary>
    public interface INativeMethods
    {
        /// <summary>
        /// Sets the DLL directory in which external dependencies can be located.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>True for success. False for failure</returns>
        bool SetDllDirectory(string path);

        /// <summary>
        /// Fast pointer memory block copy function
        /// </summary>
        /// <param name="targetAddress">The target address.</param>
        /// <param name="sourceAddress">The source address.</param>
        /// <param name="copyLength">Length of the copy.</param>
        void CopyMemory(IntPtr targetAddress, IntPtr sourceAddress, uint copyLength);

        /// <summary>
        /// Fills the memory with the specified value repeated.
        /// </summary>
        /// <param name="startAddress">The start address.</param>
        /// <param name="length">The length.</param>
        /// <param name="value">The value.</param>
        void FillMemory(IntPtr startAddress, uint length, byte value);
    }
}
