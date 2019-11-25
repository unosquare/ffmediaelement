namespace Unosquare.FFME.Primitives
{
    internal enum IntervalWorkerMode
    {
        /// <summary>
        /// Uses sleep intervals of 1 millisecond.
        /// </summary>
        ShortSleepLoop,

        /// <summary>
        /// Uses sleep intervals onf 15 milliseconds.
        /// </summary>
        DefaultSleepLoop,

        /// <summary>
        /// Uses a high-precision Windows Multimedia Timer.
        /// </summary>
        Multimedia,

        /// <summary>
        /// Uses a tight loop via context switching.
        /// </summary>
        TightLoop,
    }
}
