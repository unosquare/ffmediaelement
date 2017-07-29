#pragma warning disable IDE1006 // Naming Styles

namespace Unosquare.FFME.Rendering.Wave
{
    using System;
    using System.Runtime.InteropServices;
    
    /// <summary>
    /// MME Wave function interop
    /// </summary>
    internal class WaveInterop
    {
        // use the userdata as a reference
        // WaveOutProc http://msdn.microsoft.com/en-us/library/dd743869%28VS.85%29.aspx
        // WaveInProc http://msdn.microsoft.com/en-us/library/dd743849%28VS.85%29.aspx
        public delegate void WaveCallback(IntPtr hWaveOut, WaveMessage message, IntPtr dwInstance, WaveHeader wavhdr, IntPtr dwReserved);

        [Flags]
        public enum WaveInOutOpenFlags
        {
            /// <summary>
            /// CALLBACK_NULL
            /// No callback
            /// </summary>
            CallbackNull = 0,
            
            /// <summary>
            /// CALLBACK_FUNCTION
            /// dwCallback is a FARPROC 
            /// </summary>
            CallbackFunction = 0x30000,
            
            /// <summary>
            /// CALLBACK_EVENT
            /// dwCallback is an EVENT handle 
            /// </summary>
            CallbackEvent = 0x50000,
            
            /// <summary>
            /// CALLBACK_WINDOW
            /// dwCallback is a HWND 
            /// </summary>
            CallbackWindow = 0x10000,
            
            /// <summary>
            /// CALLBACK_THREAD
            /// callback is a thread ID 
            /// </summary>
            CallbackThread = 0x20000,
            
            /*
            WAVE_FORMAT_QUERY = 1,
            WAVE_MAPPED = 4,
            WAVE_FORMAT_DIRECT = 8
            */
        }

        /*
        public const int TIME_MS = 0x0001;  // time in milliseconds 
        public const int TIME_SAMPLES = 0x0002;  // number of wave samples 
        public const int TIME_BYTES = 0x0004;  // current byte offset 
        */

        public enum WaveMessage
        {
            /// <summary>
            /// WIM_OPEN
            /// </summary>
            WaveInOpen = 0x3BE,
            
            /// <summary>
            /// WIM_CLOSE
            /// </summary>
            WaveInClose = 0x3BF,
            
            /// <summary>
            /// WIM_DATA
            /// </summary>
            WaveInData = 0x3C0,

            /// <summary>
            /// WOM_CLOSE
            /// </summary>
            WaveOutClose = 0x3BC,
            
            /// <summary>
            /// WOM_DONE
            /// </summary>
            WaveOutDone = 0x3BD,
            
            /// <summary>
            /// WOM_OPEN
            /// </summary>
            WaveOutOpen = 0x3BB
        }

        public static class NativeMethods
        {
            private const string WinMM = "winmm.dll";

            [DllImport(WinMM)]
            public static extern Int32 waveOutGetNumDevs();

            [DllImport(WinMM)]
            public static extern MmResult waveOutPrepareHeader(IntPtr hWaveOut, WaveHeader lpWaveOutHdr, int uSize);

            [DllImport(WinMM)]
            public static extern MmResult waveOutUnprepareHeader(IntPtr hWaveOut, WaveHeader lpWaveOutHdr, int uSize);

            [DllImport(WinMM)]
            public static extern MmResult waveOutWrite(IntPtr hWaveOut, WaveHeader lpWaveOutHdr, int uSize);

            // http://msdn.microsoft.com/en-us/library/dd743866%28VS.85%29.aspx
            [DllImport(WinMM)]
            public static extern MmResult waveOutOpen(out IntPtr hWaveOut, int uDeviceID, WaveFormat lpFormat, WaveCallback dwCallback, IntPtr dwInstance, WaveInOutOpenFlags dwFlags);

            [DllImport(WinMM, EntryPoint = nameof(waveOutOpen))]
            public static extern MmResult waveOutOpenWindow(out IntPtr hWaveOut, int uDeviceID, WaveFormat lpFormat, IntPtr callbackWindowHandle, IntPtr dwInstance, WaveInOutOpenFlags dwFlags);

            [DllImport(WinMM)]
            public static extern MmResult waveOutReset(IntPtr hWaveOut);

            [DllImport(WinMM)]
            public static extern MmResult waveOutClose(IntPtr hWaveOut);

            [DllImport(WinMM)]
            public static extern MmResult waveOutPause(IntPtr hWaveOut);

            [DllImport(WinMM)]
            public static extern MmResult waveOutRestart(IntPtr hWaveOut);

            // http://msdn.microsoft.com/en-us/library/dd743863%28VS.85%29.aspx
            [DllImport(WinMM)]
            public static extern MmResult waveOutGetPosition(IntPtr hWaveOut, out MmTime mmTime, int uSize);

            // http://msdn.microsoft.com/en-us/library/dd743874%28VS.85%29.aspx
            [DllImport(WinMM)]
            public static extern MmResult waveOutSetVolume(IntPtr hWaveOut, int dwVolume);

            [DllImport(WinMM)]
            public static extern MmResult waveOutSetPitch(IntPtr hWaveOut, int dwPitch);

            [DllImport(WinMM)]
            public static extern MmResult waveOutSetPlaybackRate(IntPtr hWaveOut, int dwRate);

            [DllImport(WinMM)]
            public static extern MmResult waveOutGetVolume(IntPtr hWaveOut, out int dwVolume);

            // http://msdn.microsoft.com/en-us/library/dd743857%28VS.85%29.aspx
            [DllImport(WinMM, CharSet = CharSet.Auto)]
            public static extern MmResult waveOutGetDevCaps(IntPtr deviceID, out WaveOutCapabilities waveOutCaps, int waveOutCapsSize);
        }
    }

    /// <summary>
    /// A wrapper class for MmException.
    /// </summary>
    [Serializable]
    internal class MmException : Exception
    {
        private MmResult result;
        private string function;

        /// <summary>
        /// Initializes a new instance of the <see cref="MmException"/> class.
        /// </summary>
        /// <param name="result">The result returned by the Windows API call</param>
        /// <param name="function">The name of the Windows API that failed</param>
        public MmException(MmResult result, string function)
            : base(MmException.ErrorMessage(result, function))
        {
            this.result = result;
            this.function = function;
        }

        /// <summary>
        /// Returns the Windows API result
        /// </summary>
        public MmResult Result
        {
            get
            {
                return result;
            }
        }

        /// <summary>
        /// Helper function to automatically raise an exception on failure
        /// </summary>
        /// <param name="result">The result of the API call</param>
        /// <param name="function">The API function name</param>
        public static void Try(MmResult result, string function)
        {
            if (result != MmResult.NoError)
                throw new MmException(result, function);
        }

        /// <summary>
        /// Creates an error message base don an erro result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="function">The function.</param>
        /// <returns>A descriptive rror message</returns>
        private static string ErrorMessage(MmResult result, string function)
        {
            return String.Format("{0} calling {1}", result, function);
        }
    }
}

#pragma warning restore IDE1006 // Naming Styles