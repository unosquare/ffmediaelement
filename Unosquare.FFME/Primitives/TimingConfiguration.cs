namespace Unosquare.FFME.Primitives
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides access to advanced timing configuration.
    /// Under Windows, this class uses the Windows Multimedia API
    /// to configure timing.
    /// </summary>
    internal static class TimingConfiguration
    {
        private static readonly object SyncLock = new object();
        private static int? CurrentPeriod;

        /// <summary>
        /// Initializes static members of the <see cref="TimingConfiguration"/> class.
        /// </summary>
        static TimingConfiguration()
        {
            try
            {
                var caps = default(PeriodCapabilities);
                var result = NativeMethods.GetDeviceCapabilities(ref caps, Marshal.SizeOf<PeriodCapabilities>());
                MinimumPeriod = caps.PeriodMin;
                MaximumPeriod = caps.PeriodMax;
                IsAvailable = true;
            }
            catch
            {
                MinimumPeriod = 16;
                MaximumPeriod = 16;
                IsAvailable = false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the system supports changing timing configuration.
        /// </summary>
        public static bool IsAvailable { get; }

        /// <summary>
        /// Gets the minimum period in milliseconds.
        /// </summary>
        public static int MinimumPeriod { get; }

        /// <summary>
        /// Gets the maximum period in milliseconds.
        /// </summary>
        public static int MaximumPeriod { get; }

        /// <summary>
        /// Gets the currently configured period in milliseconds.
        /// </summary>
        public static int? Period
        {
            get
            {
                lock (SyncLock)
                    return CurrentPeriod;
            }
        }

        /// <summary>
        /// Changes configured the period in milliseconds.
        /// </summary>
        /// <param name="newPeriod">The new period.</param>
        /// <returns>If the operation was successful.</returns>
        public static bool ChangePeriod(int newPeriod)
        {
            lock (SyncLock)
            {
                if (!IsAvailable)
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

        /// <summary>
        /// Resets the period to its default.
        /// </summary>
        /// <returns>True if the operation was successful.</returns>
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

        /// <summary>
        /// A struct to exctract timing capabilities from the interop API call.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct PeriodCapabilities
        {
            public int PeriodMin;

            public int PeriodMax;
        }

        /// <summary>
        /// Provides the native methods for timing configuration.
        /// </summary>
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
