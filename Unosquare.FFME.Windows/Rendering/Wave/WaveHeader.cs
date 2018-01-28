#pragma warning disable SA1401 // Fields must be private
namespace Unosquare.FFME.Rendering.Wave
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// WaveHeader interop structure (WAVEHDR)
    /// http://msdn.microsoft.com/en-us/library/dd743837%28VS.85%29.aspx
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal class WaveHeader
    {
        /// <summary>pointer to locked data buffer (lpData)</summary>
        public IntPtr DataBuffer;

        /// <summary>length of data buffer (dwBufferLength)</summary>
        public int BufferLength;

        /// <summary>used for input only (dwBytesRecorded)</summary>
        public int BytesRecorded;

        /// <summary>for client's use (dwUser)</summary>
        public IntPtr UserData;

        /// <summary>assorted flags (dwFlags)</summary>
        public WaveHeaderFlags Flags;

        /// <summary>loop control counter (dwLoops)</summary>
        public int Loops;

        /// <summary>PWaveHdr, reserved for driver (lpNext)</summary>
        public IntPtr Next;

        /// <summary>reserved for driver</summary>
        public IntPtr Reserved;
    }
}
#pragma warning restore SA1401 // Fields must be private