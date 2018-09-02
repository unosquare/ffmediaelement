// ReSharper disable UnusedMember.Global
namespace Unosquare.FFME.Rendering.Wave
{
    using Microsoft.Win32.SafeHandles;
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    /// MME Wave function interop
    /// </summary>
    internal class WaveInterop
    {
        private const int LockTimeout = 100;
        private const string TimeoutErrorMessage = "Failed to acquire lock on MME interop call on a timely manner.";
        private static readonly object SyncLock = new object();

        // use the user data as a reference
        // WaveOutProc http://msdn.microsoft.com/en-us/library/dd743869%28VS.85%29.aspx
        // WaveInProc http://msdn.microsoft.com/en-us/library/dd743849%28VS.85%29.aspx
        public delegate void WaveCallback(IntPtr deviceHandle, WaveMessage message, IntPtr instance, WaveHeader waveHeader, IntPtr reserved);

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
            /// dwCallback is a FAR PROC
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
            CallbackThread = 0x20000

            /*
            WAVE_FORMAT_QUERY = 1,
            WAVE_MAPPED = 4,
            WAVE_FORMAT_DIRECT = 8
            */
        }

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

        #region Methods

        /// <summary>
        /// Retrieves the audio device count.
        /// </summary>
        /// <returns>The number of registered audio devices</returns>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        public static int RetrieveAudioDeviceCount()
        {
            if (!TryEnterDeviceOperation()) return 0;
            try { return NativeMethods.GetDeviceCount(); }
            finally { Monitor.Exit(SyncLock); }
        }

        /// <summary>
        /// Allocates the header.
        /// </summary>
        /// <param name="deviceHandle">The device handle.</param>
        /// <param name="header">The header.</param>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        /// <exception cref="MmException">Occurs when the MME interop call fails</exception>
        public static void AllocateHeader(IntPtr deviceHandle, WaveHeader header)
        {
            if (header == null || !TryEnterWaveOperation(deviceHandle)) return;

            try
            {
                MmException.Try(
                    NativeMethods.PrepareHeader(deviceHandle, header, Marshal.SizeOf(header)),
                    nameof(NativeMethods.PrepareHeader));
            }
            finally { Monitor.Exit(SyncLock); }
        }

        /// <summary>
        /// Releases the header.
        /// </summary>
        /// <param name="deviceHandle">The device handle.</param>
        /// <param name="header">The header.</param>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        /// <exception cref="MmException">Occurs when the MME interop call fails</exception>
        public static void ReleaseHeader(IntPtr deviceHandle, WaveHeader header)
        {
            if (header == null || !TryEnterWaveOperation(deviceHandle)) return;

            try
            {
                MmException.Try(
                    NativeMethods.ReleaseHeader(deviceHandle, header, Marshal.SizeOf(header)),
                    nameof(NativeMethods.ReleaseHeader));
            }
            finally { Monitor.Exit(SyncLock); }
        }

        /// <summary>
        /// Writes the audio data.
        /// </summary>
        /// <param name="deviceHandle">The device handle.</param>
        /// <param name="header">The header.</param>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        /// <exception cref="MmException">Occurs when the MME interop call fails</exception>
        public static void WriteAudioData(IntPtr deviceHandle, WaveHeader header)
        {
            if (header == null || !TryEnterWaveOperation(deviceHandle)) return;

            try
            {
                MmException.Try(
                    NativeMethods.WriteAudioData(deviceHandle, header, Marshal.SizeOf(header)),
                    nameof(NativeMethods.WriteAudioData));
            }
            finally { Monitor.Exit(SyncLock); }
        }

        /// <summary>
        /// Opens the audio device.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="format">The format.</param>
        /// <param name="callback">The callback.</param>
        /// <param name="instanceHandle">The instance handle.</param>
        /// <param name="openFlags">The open flags.</param>
        /// <returns>The audio device handle</returns>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        /// <exception cref="MmException">Occurs when the MME interop call fails</exception>
        public static IntPtr OpenAudioDevice(int deviceId, WaveFormat format, WaveCallback callback, IntPtr instanceHandle, WaveInOutOpenFlags openFlags)
        {
            if (deviceId < -1) throw new ArgumentException($"Invalid Device ID {deviceId}", nameof(deviceId));
            if (!TryEnterDeviceOperation())
                return IntPtr.Zero;

            try
            {
                MmException.Try(
                    NativeMethods.OpenDevice(out var hWaveOut, deviceId, format, callback, instanceHandle, openFlags),
                    nameof(NativeMethods.OpenDevice));

                return hWaveOut;
            }
            finally { Monitor.Exit(SyncLock); }
        }

        /// <summary>
        /// Opens the audio device.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="format">The format.</param>
        /// <param name="callbackHandle">The callback window handle.</param>
        /// <param name="instanceHandle">The instance handle.</param>
        /// <param name="openFlags">The open flags.</param>
        /// <returns>The audio device handle</returns>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        /// <exception cref="MmException">Occurs when the MME interop call fails</exception>
        public static IntPtr OpenAudioDevice(int deviceId, WaveFormat format, SafeWaitHandle callbackHandle, IntPtr instanceHandle, WaveInOutOpenFlags openFlags)
        {
            if (deviceId < -1) throw new ArgumentException($"Invalid Device ID {deviceId}", nameof(deviceId));
            if (!TryEnterDeviceOperation())
                return IntPtr.Zero;

            try
            {
                MmException.Try(
                    NativeMethods.OpenDeviceOnWindow(out var hWaveOut, deviceId, format, callbackHandle, instanceHandle, openFlags),
                    nameof(NativeMethods.OpenDeviceOnWindow));

                return hWaveOut;
            }
            finally { Monitor.Exit(SyncLock); }
        }

        /// <summary>
        /// Resets the audio device.
        /// </summary>
        /// <param name="deviceHandle">The device handle.</param>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        /// <exception cref="MmException">Occurs when the MME interop call fails</exception>
        public static void ResetAudioDevice(IntPtr deviceHandle)
        {
            if (!TryEnterWaveOperation(deviceHandle)) return;

            try
            {
                MmException.Try(
                    NativeMethods.ResetDevice(deviceHandle),
                    nameof(NativeMethods.ResetDevice));
            }
            finally { Monitor.Exit(SyncLock); }
        }

        /// <summary>
        /// Closes the audio device.
        /// </summary>
        /// <param name="deviceHandle">The device handle.</param>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        /// <exception cref="MmException">Occurs when the MME interop call fails</exception>
        public static void CloseAudioDevice(IntPtr deviceHandle)
        {
            if (!TryEnterWaveOperation(deviceHandle)) return;

            try
            {
                MmException.Try(
                    NativeMethods.CloseDevice(deviceHandle),
                    nameof(NativeMethods.CloseDevice));
            }
            finally { Monitor.Exit(SyncLock); }
        }

        /// <summary>
        /// Pauses the audio device.
        /// </summary>
        /// <param name="deviceHandle">The device handle.</param>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        /// <exception cref="MmException">Occurs when the MME interop call fails</exception>
        public static void PauseAudioDevice(IntPtr deviceHandle)
        {
            if (!TryEnterWaveOperation(deviceHandle)) return;

            try
            {
                MmException.Try(
                    NativeMethods.PausePlayback(deviceHandle),
                    nameof(NativeMethods.PausePlayback));
            }
            finally { Monitor.Exit(SyncLock); }
        }

        /// <summary>
        /// Restarts the audio device.
        /// </summary>
        /// <param name="deviceHandle">The device handle.</param>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        /// <exception cref="MmException">Occurs when the MME interop call fails</exception>
        public static void RestartAudioDevice(IntPtr deviceHandle)
        {
            if (!TryEnterWaveOperation(deviceHandle)) return;

            try
            {
                MmException.Try(
                    NativeMethods.RestartPlayback(deviceHandle),
                    nameof(NativeMethods.RestartPlayback));
            }
            finally { Monitor.Exit(SyncLock); }
        }

        /// <summary>
        /// Gets the playback bytes count.
        /// </summary>
        /// <param name="deviceHandle">The device handle.</param>
        /// <returns>The number of bytes played during this session</returns>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        /// <exception cref="MmException">Occurs when the MME interop call fails</exception>
        /// <exception cref="ArgumentException">Occurs when the device does not return a byte count.</exception>
        public static long GetPlaybackBytesCount(IntPtr deviceHandle)
        {
            if (!TryEnterWaveOperation(deviceHandle)) return 0;

            try
            {
                var time = new MmTime { Type = MmTime.TimeBytes };
                var structSize = Marshal.SizeOf(time);

                MmException.Try(
                    NativeMethods.GetPlaybackPosition(deviceHandle, out time, structSize),
                    nameof(NativeMethods.GetPlaybackPosition));

                if (time.Type != MmTime.TimeBytes)
                {
                    throw new ArgumentException($"{nameof(NativeMethods.GetPlaybackPosition)}: "
                        + $"wType -> Expected {nameof(MmTime.TimeBytes)}, Received {time.Type}");
                }

                return time.CB;
            }
            finally { Monitor.Exit(SyncLock); }
        }

        /// <summary>
        /// Retrieves the audio device information.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <returns>The audio device capabilities and metadata</returns>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        /// <exception cref="MmException">Occurs when the MME interop call fails</exception>
        public static LegacyAudioDeviceInfo RetrieveAudioDeviceInfo(int deviceId)
        {
            if (deviceId < -1) throw new ArgumentException($"Invalid Device ID {deviceId}", nameof(deviceId));
            if (!TryEnterDeviceOperation())
                return default;

            try
            {
                MmException.Try(
                    NativeMethods.RetrieveDeviceCapabilities((IntPtr)deviceId,
                        out var waveOutCaps,
                        Marshal.SizeOf(typeof(LegacyAudioDeviceInfo))),
                    nameof(NativeMethods.RetrieveDeviceCapabilities));

                return waveOutCaps;
            }
            finally { Monitor.Exit(SyncLock); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryEnterWaveOperation(IntPtr deviceHandle)
        {
            if (deviceHandle == IntPtr.Zero) return false;
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            return acquired ? true : throw new TimeoutException(TimeoutErrorMessage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryEnterDeviceOperation()
        {
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            return acquired ? true : throw new TimeoutException(TimeoutErrorMessage);
        }

        #endregion

        /// <summary>
        /// Contains the native methods for the Windows MME API
        /// </summary>
        private static class NativeMethods
        {
            private const string WinMM = "winmm.dll";

            [DllImport(WinMM, EntryPoint = "waveOutGetNumDevs")]
            public static extern int GetDeviceCount();

            [DllImport(WinMM, EntryPoint = "waveOutPrepareHeader")]
            public static extern MmResult PrepareHeader(IntPtr deviceHandle, WaveHeader header, int headerSize);

            [DllImport(WinMM, EntryPoint = "waveOutUnprepareHeader")]
            public static extern MmResult ReleaseHeader(IntPtr deviceHandle, WaveHeader header, int headerSize);

            [DllImport(WinMM, EntryPoint = "waveOutWrite")]
            public static extern MmResult WriteAudioData(IntPtr deviceHandle, WaveHeader header, int headerSize);

            // http://msdn.microsoft.com/en-us/library/dd743866%28VS.85%29.aspx
            [DllImport(WinMM, EntryPoint = "waveOutOpen")]
            public static extern MmResult OpenDevice(
                out IntPtr deviceHandle, int deviceId, WaveFormat waveFormat, WaveCallback callbackMethod, IntPtr instanceHandle, WaveInOutOpenFlags openFlags);

            [DllImport(WinMM, EntryPoint = "waveOutOpen")]
            public static extern MmResult OpenDeviceOnWindow(
                out IntPtr deviceHandle, int deviceId, WaveFormat waveFormat, SafeWaitHandle callbackHandle, IntPtr instanceHandle, WaveInOutOpenFlags openFlags);

            [DllImport(WinMM, EntryPoint = "waveOutReset")]
            public static extern MmResult ResetDevice(IntPtr deviceHandle);

            [DllImport(WinMM, EntryPoint = "waveOutClose")]
            public static extern MmResult CloseDevice(IntPtr deviceHandle);

            [DllImport(WinMM, EntryPoint = "waveOutPause")]
            public static extern MmResult PausePlayback(IntPtr deviceHandle);

            [DllImport(WinMM, EntryPoint = "waveOutRestart")]
            public static extern MmResult RestartPlayback(IntPtr deviceHandle);

            // http://msdn.microsoft.com/en-us/library/dd743863%28VS.85%29.aspx
            [DllImport(WinMM, EntryPoint = "waveOutGetPosition")]
            public static extern MmResult GetPlaybackPosition(IntPtr deviceHandle, out MmTime mmTime, int mmTimeSize);

            // http://msdn.microsoft.com/en-us/library/dd743857%28VS.85%29.aspx
            [DllImport(WinMM, EntryPoint = "waveOutGetDevCaps", CharSet = CharSet.Auto)]
            public static extern MmResult RetrieveDeviceCapabilities(IntPtr deviceId, out LegacyAudioDeviceInfo waveOutCaps, int waveOutCapsSize);
        }
    }
}
