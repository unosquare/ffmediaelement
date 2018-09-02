﻿// ReSharper disable UnusedMember.Global
namespace Unosquare.FFME.Rendering.Wave
{
    using System;
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
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            if (acquired == false) throw new TimeoutException(TimeoutErrorMessage);

            try { return NativeMethods.waveOutGetNumDevs(); }
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
            if (deviceHandle == IntPtr.Zero) return;
            if (header == null) return;
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            if (acquired == false) throw new TimeoutException(TimeoutErrorMessage);

            try
            {
                MmException.Try(
                    NativeMethods.waveOutPrepareHeader(deviceHandle, header, Marshal.SizeOf(header)),
                    nameof(NativeMethods.waveOutPrepareHeader));
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
            if (deviceHandle == IntPtr.Zero) return;
            if (header == null) return;
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            if (acquired == false) throw new TimeoutException(TimeoutErrorMessage);

            try
            {
                MmException.Try(
                    NativeMethods.waveOutUnprepareHeader(deviceHandle, header, Marshal.SizeOf(header)),
                    nameof(NativeMethods.waveOutUnprepareHeader));
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
            if (deviceHandle == IntPtr.Zero) return;
            if (header == null) return;
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            if (acquired == false) throw new TimeoutException(TimeoutErrorMessage);

            try
            {
                MmException.Try(
                    NativeMethods.waveOutWrite(deviceHandle, header, Marshal.SizeOf(header)),
                    nameof(NativeMethods.waveOutWrite));
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
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            if (acquired == false) throw new TimeoutException(TimeoutErrorMessage);

            try
            {
                MmException.Try(
                    NativeMethods.waveOutOpen(out var hWaveOut, deviceId, format, callback, instanceHandle, openFlags),
                    nameof(NativeMethods.waveOutOpen));

                return hWaveOut;
            }
            finally { Monitor.Exit(SyncLock); }
        }

        /// <summary>
        /// Opens the audio device.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="format">The format.</param>
        /// <param name="callbackWindowHandle">The callback window handle.</param>
        /// <param name="instanceHandle">The instance handle.</param>
        /// <param name="openFlags">The open flags.</param>
        /// <returns>The audio device handle</returns>
        /// <exception cref="TimeoutException">Occurs when the interop lock cannot be acquired.</exception>
        /// <exception cref="MmException">Occurs when the MME interop call fails</exception>
        public static IntPtr OpenAudioDevice(int deviceId, WaveFormat format, IntPtr callbackWindowHandle, IntPtr instanceHandle, WaveInOutOpenFlags openFlags)
        {
            if (deviceId < -1) throw new ArgumentException($"Invalid Device ID {deviceId}", nameof(deviceId));
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            if (acquired == false) throw new TimeoutException(TimeoutErrorMessage);

            try
            {
                MmException.Try(
                    NativeMethods.waveOutOpenWindow(out var hWaveOut, deviceId, format, callbackWindowHandle, instanceHandle, openFlags),
                    nameof(NativeMethods.waveOutOpenWindow));

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
            if (deviceHandle == IntPtr.Zero) return;
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            if (acquired == false) throw new TimeoutException(TimeoutErrorMessage);

            try
            {
                MmException.Try(
                    NativeMethods.waveOutReset(deviceHandle),
                    nameof(NativeMethods.waveOutReset));
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
            if (deviceHandle == IntPtr.Zero) return;
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            if (acquired == false) throw new TimeoutException(TimeoutErrorMessage);

            try
            {
                MmException.Try(
                    NativeMethods.waveOutClose(deviceHandle),
                    nameof(NativeMethods.waveOutClose));
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
            if (deviceHandle == IntPtr.Zero) return;
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            if (acquired == false) throw new TimeoutException(TimeoutErrorMessage);

            try
            {
                MmException.Try(
                    NativeMethods.waveOutPause(deviceHandle),
                    nameof(NativeMethods.waveOutPause));
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
            if (deviceHandle == IntPtr.Zero) return;
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            if (acquired == false) throw new TimeoutException(TimeoutErrorMessage);

            try
            {
                MmException.Try(
                    NativeMethods.waveOutRestart(deviceHandle),
                    nameof(NativeMethods.waveOutRestart));
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
            if (deviceHandle == IntPtr.Zero) return 0;
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            if (acquired == false) throw new TimeoutException(TimeoutErrorMessage);

            try
            {
                var time = new MmTime { Type = MmTime.TimeBytes };
                var structSize = Marshal.SizeOf(time);

                MmException.Try(
                    NativeMethods.waveOutGetPosition(deviceHandle, out time, structSize),
                    nameof(NativeMethods.waveOutGetPosition));

                if (time.Type != MmTime.TimeBytes)
                {
                    throw new ArgumentException($"{nameof(NativeMethods.waveOutGetPosition)}: "
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
            var acquired = false;
            Monitor.TryEnter(SyncLock, LockTimeout, ref acquired);
            if (acquired == false) throw new TimeoutException(TimeoutErrorMessage);

            try
            {
                MmException.Try(
                    NativeMethods.waveOutGetDevCaps((IntPtr)deviceId,
                        out var waveOutCaps,
                        Marshal.SizeOf(typeof(LegacyAudioDeviceInfo))),
                    nameof(NativeMethods.waveOutGetDevCaps));

                return waveOutCaps;
            }
            finally { Monitor.Exit(SyncLock); }
        }

        #endregion

        /// <summary>
        /// Contains the native methods for the Windows MME API
        /// </summary>
        private static class NativeMethods
        {
#pragma warning disable IDE1006 // Naming Styles

            private const string WinMM = "winmm.dll";

            [DllImport(WinMM)]
            public static extern int waveOutGetNumDevs();

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

            // http://msdn.microsoft.com/en-us/library/dd743857%28VS.85%29.aspx
            [DllImport(WinMM, CharSet = CharSet.Auto)]
            public static extern MmResult waveOutGetDevCaps(IntPtr deviceID, out LegacyAudioDeviceInfo waveOutCaps, int waveOutCapsSize);

#pragma warning restore IDE1006 // Naming Styles
        }
    }
}
