namespace Unosquare.FFME.Primitives
{
    internal enum IntervalWorkerMode
    {
        /// <summary>
        /// Uses a cancellation token together with a wait handle.
        /// PRecision depends on system parameters.
        /// </summary>
        SystemDefault,

        /// <summary>
        /// Uses thread sleeping and spinning to achieve a higher resolution
        /// at the cost of CPU usage.
        /// </summary>
        HighPrecision,
    }
}
