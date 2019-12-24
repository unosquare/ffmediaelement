namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    /// Vertical Synchronization provider.
    /// Ideas taken from:
    /// 1. https://github.com/fuse-open/fuse-studio/blob/master/Source/Fusion/Windows/VerticalSynchronization.cs.
    /// 2. https://gist.github.com/anonymous/4397e4909c524c939bee.
    /// Related Discussion:
    /// https://bugs.chromium.org/p/chromium/issues/detail?id=467617.
    /// </summary>
    internal sealed class VerticalSyncContext : IDisposable
    {
        private static readonly object NativeSyncLock = new object();
        private readonly Stopwatch RefreshStopwatch = Stopwatch.StartNew();
        private readonly object SyncLock = new object();
        private bool IsDisposed;
        private DisplayDeviceInfo? DisplayDevice;
        private AdapterInfo CurrentAdapterInfo;
        private VerticalSyncEventInfo VerticalSyncEvent;
        private double RefreshCount;

        /// <summary>
        /// Initializes static members of the <see cref="VerticalSyncContext"/> class.
        /// </summary>
        static VerticalSyncContext()
        {
            IsAvailable = PrimaryDisplayDevice != null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VerticalSyncContext"/> class.
        /// </summary>
        public VerticalSyncContext()
        {
            lock (SyncLock)
            {
                EnsureAdapter();
            }
        }

        /// <summary>
        /// Enumerates the device state flags.
        /// </summary>
        [Flags]
        private enum DisplayDeviceStateFlags : int
        {
            AttachedToDesktop = 0x1,
            MultiDriver = 0x2,
            PrimaryDevice = 0x4,
            MirroringDriver = 0x8,
            VGACompatible = 0x10,
            Removable = 0x20,
            ModesPruned = 0x8000000,
            Remote = 0x4000000,
            Disconnect = 0x2000000
        }

        /// <summary>
        /// Gets a value indicating whether Vertical Synchronization is available on the system.
        /// </summary>
        public static bool IsAvailable { get; private set; }

        /// <summary>
        /// Gets the display device refresh rate in Hz.
        /// </summary>
        public double RefreshRateHz { get; private set; } = 60;

        /// <summary>
        /// Gets the refresh period of the display device.
        /// </summary>
        public TimeSpan RefreshPeriod => TimeSpan.FromSeconds(1d / RefreshRateHz);

        /// <summary>
        /// Gets the display devices.
        /// </summary>
        private static DisplayDeviceInfo[] DisplayDevices
        {
            get
            {
                lock (NativeSyncLock)
                {
                    var structSize = Marshal.SizeOf<DisplayDeviceInfo>();
                    var result = new List<DisplayDeviceInfo>(16);

                    try
                    {
                        var deviceIndex = 0u;
                        while (true)
                        {
                            var d = default(DisplayDeviceInfo);
                            d.StructSize = structSize;
                            if (!NativeMethods.EnumDisplayDevices(null, deviceIndex, ref d, 0))
                                break;

                            result.Add(d);
                            deviceIndex++;
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    return result.ToArray();
                }
            }
        }

        /// <summary>
        /// Gets the primary display device.
        /// </summary>
        private static DisplayDeviceInfo? PrimaryDisplayDevice
        {
            get
            {
                var devices = DisplayDevices;
                if (devices.Length == 0 || !devices.Any(d => d.StateFlags.HasFlag(DisplayDeviceStateFlags.PrimaryDevice)))
                    return null;

                return devices.First(d => d.StateFlags.HasFlag(DisplayDeviceStateFlags.PrimaryDevice));
            }
        }

        /// <summary>
        /// An alternative, less precise method of waiting for the monitor's vertical synchronization.
        /// </summary>
        public static void Flush() => NativeMethods.DwmFlush();

        public void Wait()
        {
            lock (SyncLock)
            {
                try
                {
                    if (!IsAvailable || !EnsureAdapter())
                    {
                        Thread.Sleep(Constants.DefaultTimingPeriod);
                        return;
                    }

                    try
                    {
                        var waitResult = NativeMethods.D3DKMTWaitForVerticalBlankEvent(ref VerticalSyncEvent);
                        if (waitResult != 0)
                            throw new Exception("Adapter needs to be recreated. Resources will be released.");
                    }
                    catch
                    {
                        ReleaseAdapter();
                    }
                }
                finally
                {
                    RefreshCount++;

                    if (RefreshStopwatch.Elapsed.TotalMilliseconds >= 1000)
                    {
                        RefreshRateHz = RefreshCount / RefreshStopwatch.Elapsed.TotalSeconds;
                        RefreshStopwatch.Restart();
                        RefreshCount = 0;
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (SyncLock)
            {
                if (IsDisposed) return;
                IsDisposed = true;

                ReleaseAdapter();
            }
        }

        private bool EnsureAdapter()
        {
            if (DisplayDevice == null)
            {
                DisplayDevice = PrimaryDisplayDevice;
                if (DisplayDevice == null)
                {
                    IsAvailable = false;
                    return false;
                }
            }

            if (CurrentAdapterInfo.DCHandle == IntPtr.Zero)
            {
                try
                {
                    CurrentAdapterInfo = default;
                    CurrentAdapterInfo.DCHandle = NativeMethods.CreateDC(DisplayDevice.Value.DeviceName, null, null, IntPtr.Zero);
                    if (CurrentAdapterInfo.DCHandle == IntPtr.Zero)
                        throw new NotSupportedException("Unable to create DC for adapter.");
                }
                catch
                {
                    ReleaseAdapter();
                    IsAvailable = false;
                    return false;
                }
            }

            if (VerticalSyncEvent.AdapterHandle == 0 && CurrentAdapterInfo.DCHandle != IntPtr.Zero)
            {
                try
                {
                    var openAdapterResult = NativeMethods.D3DKMTOpenAdapterFromHdc(ref CurrentAdapterInfo);
                    if (openAdapterResult == 0)
                    {
                        VerticalSyncEvent = default;
                        VerticalSyncEvent.AdapterHandle = CurrentAdapterInfo.AdapterHandle;
                        VerticalSyncEvent.DeviceHandle = 0;
                        VerticalSyncEvent.PresentSourceId = CurrentAdapterInfo.PresentSourceId;
                    }
                    else
                    {
                        throw new NotSupportedException("Unable to open D3D adapter.");
                    }
                }
                catch
                {
                    ReleaseAdapter();
                    IsAvailable = false;
                    return false;
                }
            }

            return VerticalSyncEvent.AdapterHandle != 0;
        }

        private void ReleaseAdapter()
        {
            if (CurrentAdapterInfo.AdapterHandle != 0)
            {
                try
                {
                    var closeInfo = default(CloseAdapterInfo);
                    closeInfo.AdapterHandle = CurrentAdapterInfo.AdapterHandle;

                    // This will return 0 on success, and another value for failure.
                    var closeAdapterResult = NativeMethods.D3DKMTCloseAdapter(ref closeInfo);
                }
                catch
                {
                    // ignore
                }
            }

            if (CurrentAdapterInfo.DCHandle != IntPtr.Zero)
            {
                try
                {
                    // this will return 1 on success, 0 on failure.
                    var deleteContextResult = NativeMethods.DeleteDC(CurrentAdapterInfo.DCHandle);
                }
                catch
                {
                    // ignore
                }
            }

            DisplayDevice = null;
            CurrentAdapterInfo = default;
            VerticalSyncEvent = default;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AdapterInfo
        {
            public IntPtr DCHandle;
            public uint AdapterHandle;
            public uint AdapterLuidLowPart;
            public uint AdapterLuidHighPart;
            public uint PresentSourceId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VerticalSyncEventInfo
        {
            public uint AdapterHandle;
            public uint DeviceHandle;
            public uint PresentSourceId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CloseAdapterInfo
        {
            public uint AdapterHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DisplayDeviceInfo
        {
            [MarshalAs(UnmanagedType.U4)]
            public int StructSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            [MarshalAs(UnmanagedType.U4)]
            public DisplayDeviceStateFlags StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        private static class NativeMethods
        {
            private const string GDI32 = "Gdi32.dll";
            private const string USER32 = "User32.dll";
            private const string DWMAPI = "DwmApi.dll";

            [DllImport(USER32, CharSet = CharSet.Unicode)]
            public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DisplayDeviceInfo lpDisplayDevice, uint dwFlags);

            [DllImport(GDI32, CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

            [DllImport(USER32)]
            public static extern IntPtr GetDesktopWindow();

            [DllImport(USER32)]
            public static extern IntPtr GetDC(IntPtr windowHandle);

            [DllImport(GDI32)]
            public static extern uint DeleteDC(IntPtr deviceContextHandle);

            [DllImport(GDI32)]
            public static extern uint D3DKMTOpenAdapterFromHdc(ref AdapterInfo adapterInfo);

            [DllImport(GDI32)]
            public static extern uint D3DKMTWaitForVerticalBlankEvent(ref VerticalSyncEventInfo eventInfo);

            [DllImport(GDI32)]
            public static extern uint D3DKMTCloseAdapter(ref CloseAdapterInfo adapterInfo);

            [DllImport(DWMAPI)]
            public static extern void DwmFlush();
        }
    }
}
