namespace Unosquare.FFME.Rendering
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;

    /// <summary>
    /// Vertical Synchronization provider.
    /// Ideas taken from:
    /// 1. https://github.com/fuse-open/fuse-studio/blob/master/Source/Fusion/Windows/VerticalSynchronization.cs.
    /// 2. https://gist.github.com/anonymous/4397e4909c524c939bee.
    /// Related Discussion:
    /// https://bugs.chromium.org/p/chromium/issues/detail?id=467617.
    /// </summary>
    internal static class VerticalSyncContext
    {
        [Obsolete("TODO: This is supposedly a more precise way of synchronizing but I need more time for testing.")]
        public static bool Wait()
        {
            // TODO: Try implementing this method. Obviosuly we don't want to create and release on every cycle
            // but this was just a test.
            var adapterInfo = default(AdapterInfo);
            adapterInfo.DCHandle = NativeMethods.CreateDC(Screen.PrimaryScreen.DeviceName, null, null, IntPtr.Zero);

            // The results for these APIs are of type NTSTATUS
            // Typically 0 for success. NTSTATUS has ranges of codes.
            // See: https://docs.microsoft.com/en-us/windows-hardware/drivers/kernel/using-ntstatus-values.
            var result = NativeMethods.D3DKMTOpenAdapterFromHdc(ref adapterInfo);
            var eventInfo = default(VerticalSyncEventInfo);
            eventInfo.AdapterHandle = adapterInfo.AdapterHandle;
            eventInfo.DeviceHandle = 0;
            eventInfo.PresentSourceId = adapterInfo.PresentSourceId;

            result = NativeMethods.D3DKMTWaitForVerticalBlankEvent(ref eventInfo);

            var closeInfo = default(CloseAdapterInfo);
            closeInfo.AdapterHandle = adapterInfo.AdapterHandle;
            result = NativeMethods.D3DKMTCloseAdapter(ref closeInfo);

            // this will return 1 on success, 0 on failure.
            result = NativeMethods.DeleteDC(adapterInfo.DCHandle);
            return result == 0;
        }

        public static void Flush() => NativeMethods.DwmFlush();

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

        private static class NativeMethods
        {
            private const string GDI32 = "Gdi32.dll";
            private const string USER32 = "User32.dll";
            private const string DWMAPI = "DwmApi.dll";

            [DllImport(GDI32, CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

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
