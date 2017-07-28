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

        // use the userdata as a reference
        // WaveOutProc http://msdn.microsoft.com/en-us/library/dd743869%28VS.85%29.aspx
        // WaveInProc http://msdn.microsoft.com/en-us/library/dd743849%28VS.85%29.aspx
        public delegate void WaveCallback(IntPtr hWaveOut, WaveMessage message, IntPtr dwInstance, WaveHeader wavhdr, IntPtr dwReserved);

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

    /// <summary>
    /// Windows multimedia error codes from mmsystem.h.
    /// </summary>
    internal enum MmResult
    {
        /// <summary>no error, MMSYSERR_NOERROR</summary>
        NoError = 0,
        
        /// <summary>unspecified error, MMSYSERR_ERROR</summary>
        UnspecifiedError = 1,
        
        /// <summary>device ID out of range, MMSYSERR_BADDEVICEID</summary>
        BadDeviceId = 2,
        
        /// <summary>driver failed enable, MMSYSERR_NOTENABLED</summary>
        NotEnabled = 3,
        
        /// <summary>device already allocated, MMSYSERR_ALLOCATED</summary>
        AlreadyAllocated = 4,
        
        /// <summary>device handle is invalid, MMSYSERR_INVALHANDLE</summary>
        InvalidHandle = 5,
        
        /// <summary>no device driver present, MMSYSERR_NODRIVER</summary>
        NoDriver = 6,
        
        /// <summary>memory allocation error, MMSYSERR_NOMEM</summary>
        MemoryAllocationError = 7,
        
        /// <summary>function isn't supported, MMSYSERR_NOTSUPPORTED</summary>
        NotSupported = 8,
        
        /// <summary>error value out of range, MMSYSERR_BADERRNUM</summary>
        BadErrorNumber = 9,
        
        /// <summary>invalid flag passed, MMSYSERR_INVALFLAG</summary>
        InvalidFlag = 10,
        
        /// <summary>invalid parameter passed, MMSYSERR_INVALPARAM</summary>
        InvalidParameter = 11,
        
        /// <summary>handle being used simultaneously on another thread (eg callback),MMSYSERR_HANDLEBUSY</summary>
        HandleBusy = 12,
        
        /// <summary>specified alias not found, MMSYSERR_INVALIDALIAS</summary>
        InvalidAlias = 13,
        
        /// <summary>bad registry database, MMSYSERR_BADDB</summary>
        BadRegistryDatabase = 14,
        
        /// <summary>registry key not found, MMSYSERR_KEYNOTFOUND</summary>
        RegistryKeyNotFound = 15,
        
        /// <summary>registry read error, MMSYSERR_READERROR</summary>
        RegistryReadError = 16,
        
        /// <summary>registry write error, MMSYSERR_WRITEERROR</summary>
        RegistryWriteError = 17,
        
        /// <summary>registry delete error, MMSYSERR_DELETEERROR</summary>
        RegistryDeleteError = 18,
        
        /// <summary>registry value not found, MMSYSERR_VALNOTFOUND</summary>
        RegistryValueNotFound = 19,
        
        /// <summary>driver does not call DriverCallback, MMSYSERR_NODRIVERCB</summary>
        NoDriverCallback = 20,
        
        /// <summary>more data to be returned, MMSYSERR_MOREDATA</summary>
        MoreData = 21,

        /// <summary>unsupported wave format, WAVERR_BADFORMAT</summary>
        WaveBadFormat = 32,
        
        /// <summary>still something playing, WAVERR_STILLPLAYING</summary>
        WaveStillPlaying = 33,
        
        /// <summary>header not prepared, WAVERR_UNPREPARED</summary>
        WaveHeaderUnprepared = 34,
        
        /// <summary>device is synchronous, WAVERR_SYNC</summary>
        WaveSync = 35,

        // ACM error codes, found in msacm.h

        /// <summary>Conversion not possible (ACMERR_NOTPOSSIBLE)</summary>
        AcmNotPossible = 512,
        
        /// <summary>Busy (ACMERR_BUSY)</summary>
        AcmBusy = 513,
        
        /// <summary>Header Unprepared (ACMERR_UNPREPARED)</summary>
        AcmHeaderUnprepared = 514,
        
        /// <summary>Cancelled (ACMERR_CANCELED)</summary>
        AcmCancelled = 515,

        // Mixer error codes, found in mmresult.h

        /// <summary>invalid line (MIXERR_INVALLINE)</summary>
        MixerInvalidLine = 1024,
        
        /// <summary>invalid control (MIXERR_INVALCONTROL)</summary>
        MixerInvalidControl = 1025,
        
        /// <summary>invalid value (MIXERR_INVALVALUE)</summary>
        MixerInvalidValue = 1026,
    }

    /// <summary>
    /// http://msdn.microsoft.com/en-us/library/dd757347(v=VS.85).aspx
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct MmTime
    {
        public const int TIME_MS = 0x0001;
        public const int TIME_SAMPLES = 0x0002;
        public const int TIME_BYTES = 0x0004;

        [FieldOffset(0)]
        public UInt32 wType;
        [FieldOffset(4)]
        public UInt32 ms;
        [FieldOffset(4)]
        public UInt32 sample;
        [FieldOffset(4)]
        public UInt32 cb;
        [FieldOffset(4)]
        public UInt32 ticks;
        [FieldOffset(4)]
        public Byte smpteHour;
        [FieldOffset(5)]
        public Byte smpteMin;
        [FieldOffset(6)]
        public Byte smpteSec;
        [FieldOffset(7)]
        public Byte smpteFrame;
        [FieldOffset(8)]
        public Byte smpteFps;
        [FieldOffset(9)]
        public Byte smpteDummy;
        [FieldOffset(10)]
        public Byte smptePad0;
        [FieldOffset(11)]
        public Byte smptePad1;
        [FieldOffset(4)]
        public UInt32 midiSongPtrPos;
    }

    /// <summary>
    /// Flags indicating what features this WaveOut device supports
    /// </summary>
    [Flags]
    internal enum WaveOutSupport
    {
        /// <summary>supports pitch control (WAVECAPS_PITCH)</summary>
        Pitch = 0x0001,
        
        /// <summary>supports playback rate control (WAVECAPS_PLAYBACKRATE)</summary>
        PlaybackRate = 0x0002,
        
        /// <summary>supports volume control (WAVECAPS_VOLUME)</summary>
        Volume = 0x0004,
        
        /// <summary>supports separate left-right volume control (WAVECAPS_LRVOLUME)</summary>
        LRVolume = 0x0008,
        
        /// <summary>(WAVECAPS_SYNC)</summary>
        Sync = 0x0010,
        
        /// <summary>(WAVECAPS_SAMPLEACCURATE)</summary>
        SampleAccurate = 0x0020,
    }
}

#pragma warning restore IDE1006 // Naming Styles