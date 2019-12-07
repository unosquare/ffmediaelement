namespace Unosquare.FFME.Primitives
{
    /// <summary>
    /// Enumerates the different worker modes.
    /// </summary>
    internal enum IntervalWorkerMode
    {
        /// <summary>
        /// Uses a standard threading timer to determine the time
        /// when code needs to be executed.
        /// </summary>
        SystemDefault,

        /// <summary>
        /// Tries to use a multimedia timer together with looping to acocmplish
        /// a precise execution interval.
        /// </summary>
        HighPrecision,
    }
}
