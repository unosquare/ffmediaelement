namespace Unosquare.FFME.Primitives
{
    using System.Runtime.InteropServices;

    internal static class TimingConfiguration
    {
        private static readonly object SyncLock = new object();
        private static int? CurrentPeriod;

        static TimingConfiguration()
        {
            try
            {
                var caps = default(PeriodCapabilities);
                var result = NativeMethods.GetDeviceCapabilities(ref caps, Marshal.SizeOf<PeriodCapabilities>());
                MinimumPeriod = caps.PeriodMin;
                MaximumPeriod = caps.PeriodMax;
                IsHighResolution = true;
            }
            catch
            {
                MinimumPeriod = 16;
                MaximumPeriod = 16;
                IsHighResolution = false;
            }
        }

        public static bool IsHighResolution { get; }

        public static int MinimumPeriod { get; }

        public static int MaximumPeriod { get; }

        public static int? Period
        {
            get
            {
                lock (SyncLock)
                    return CurrentPeriod;
            }
        }

        public static bool ChangePeriod(int newPeriod)
        {
            lock (SyncLock)
            {
                if (!IsHighResolution)
                    return false;

                if (CurrentPeriod.HasValue && CurrentPeriod.Value == newPeriod)
                    return true;

                ResetPeriod();
                var success = NativeMethods.BeginUsingPeriod(newPeriod) == 0;
                if (success)
                    CurrentPeriod = newPeriod;

                return success;
            }
        }

        public static bool ResetPeriod()
        {
            lock (SyncLock)
            {
                if (!CurrentPeriod.HasValue)
                    return false;

                var success = NativeMethods.EndUsingPeriod(CurrentPeriod.Value) == 0;
                if (success)
                    CurrentPeriod = null;

                return success;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PeriodCapabilities
        {
            public int PeriodMin;

            public int PeriodMax;
        }

        private static class NativeMethods
        {
            private const string WinMM = "winmm.dll";

            [DllImport(WinMM, EntryPoint = "timeGetDevCaps")]
            public static extern int GetDeviceCapabilities(ref PeriodCapabilities ptc, int cbtc);

            [DllImport(WinMM, EntryPoint = "timeBeginPeriod")]
            public static extern int BeginUsingPeriod(int periodMillis);

            [DllImport(WinMM, EntryPoint = "timeEndPeriod")]
            public static extern int EndUsingPeriod(int periodMillis);
        }
    }
}
