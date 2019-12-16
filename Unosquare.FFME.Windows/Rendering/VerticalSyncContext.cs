#pragma warning disable CA1812
namespace Unosquare.FFME.Rendering
{
    using System;
    using System.Collections.Generic;
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
        private readonly object SyncLock = new object();
        private bool IsDisposed;
        private AdapterInfo CurrentAdapterInfo;
        private bool IsAdapterOpen;
        private VerticalSyncEventInfo VerticalSyncEvent;

        public VerticalSyncContext()
        {
            lock (SyncLock)
            {
                EnsureAdapter();
            }
        }

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

        public static void Flush() => NativeMethods.DwmFlush();

        public void Wait()
        {
            lock (SyncLock)
            {
                EnsureAdapter();

                if (!IsAdapterOpen)
                {
                    Thread.Sleep(1);
                    return;
                }

                var waitResult = NativeMethods.D3DKMTWaitForVerticalBlankEvent(ref VerticalSyncEvent);
                if (waitResult != 0)
                {
                    ReleaseAdapter();
                    IsAdapterOpen = false;
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

        private static DisplayDeviceInfo[] EnumerateDisplayDevices()
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

        private bool EnsureAdapter()
        {
            if (IsAdapterOpen)
                return true;

            var displayDevices = EnumerateDisplayDevices();
            if (displayDevices.Length == 0)
            {
                ReleaseAdapter();
                return false;
            }

            var primaryDisplayName = displayDevices.FirstOrDefault(d => d.StateFlags.HasFlag(DisplayDeviceStateFlags.PrimaryDevice)).DeviceName;
            CurrentAdapterInfo = default;
            CurrentAdapterInfo.DCHandle = NativeMethods.CreateDC(primaryDisplayName, null, null, IntPtr.Zero);

            var openAdapterResult = NativeMethods.D3DKMTOpenAdapterFromHdc(ref CurrentAdapterInfo);
            if (openAdapterResult == 0)
            {
                IsAdapterOpen = true;
                VerticalSyncEvent = default;
                VerticalSyncEvent.AdapterHandle = CurrentAdapterInfo.AdapterHandle;
                VerticalSyncEvent.DeviceHandle = 0;
                VerticalSyncEvent.PresentSourceId = CurrentAdapterInfo.PresentSourceId;
            }
            else
            {
                IsAdapterOpen = false;
                VerticalSyncEvent = default;
            }

            return IsAdapterOpen;
        }

        private bool ReleaseAdapter()
        {
            if (!IsAdapterOpen)
                return true;

            var closeInfo = default(CloseAdapterInfo);
            closeInfo.AdapterHandle = CurrentAdapterInfo.AdapterHandle;
            var closeAdapterResult = NativeMethods.D3DKMTCloseAdapter(ref closeInfo);

            // this will return 1 on success, 0 on failure.
            var deleteContextResult = NativeMethods.DeleteDC(CurrentAdapterInfo.DCHandle);

            IsAdapterOpen = false;
            return deleteContextResult != 0 && closeAdapterResult == 0;
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
#pragma warning restore CA1812