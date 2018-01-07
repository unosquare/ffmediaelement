namespace Unosquare.FFME.Shared
{
    using FFmpeg.AutoGen;
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Provides various helpers and extension methods.
    /// </summary>
    public static class Extensions
    {
        #region Output Debugging

        /// <summary>
        /// Returns a formatted timestamp string in Seconds
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <returns>The formatted string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format(this TimeSpan ts)
        {
            if (ts == TimeSpan.MinValue)
                return $"{"N/A",10}";
            else
                return $"{ts.TotalSeconds,10:0.000}";
        }

        /// <summary>
        /// Returns a formatted string with elapsed milliseconds between now and
        /// the specified date.
        /// </summary>
        /// <param name="dt">The dt.</param>
        /// <returns>The formatted string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string FormatElapsed(this DateTime dt)
        {
            return $"{DateTime.UtcNow.Subtract(dt).TotalMilliseconds,6:0}";
        }

        /// <summary>
        /// Returns a fromatted string, dividing by the specified
        /// factor. Useful for debugging longs with byte positions or sizes.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="divideBy">The divide by.</param>
        /// <returns>The formatted string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format(this long ts, double divideBy = 1)
        {
            return divideBy == 1 ? $"{ts,10:#,##0}" : $"{ts / divideBy,10:#,##0.000}";
        }

        #endregion

        #region Math 

        /// <summary>
        /// Converts the given value to a value that is of the given multiple. 
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="multiple">The multiple.</param>
        /// <returns>The value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ToMultipleOf(this double value, double multiple)
        {
            var factor = (int)(value / multiple);
            return factor * multiple;
        }

        /// <summary>
        /// Gets a timespan given a timestamp and a timebase.
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns>The TimeSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ToTimeSpan(this double pts, AVRational timeBase)
        {
            if (double.IsNaN(pts) || pts == ffmpeg.AV_NOPTS_VALUE)
                return TimeSpan.MinValue;

            if (timeBase.den == 0)
                return TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * 1000 * pts / ffmpeg.AV_TIME_BASE, 0));

            return TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * 1000 * pts * timeBase.num / timeBase.den, 0));
        }

        /// <summary>
        /// Gets a timespan given a timestamp and a timebase.
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns>The TimeSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ToTimeSpan(this long pts, AVRational timeBase)
        {
            return ((double)pts).ToTimeSpan(timeBase);
        }

        /// <summary>
        /// Gets a timespan given a timestamp and a timebase.
        /// </summary>
        /// <param name="pts">The PTS in seconds.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns>The TimeSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ToTimeSpan(this double pts, double timeBase)
        {
            if (double.IsNaN(pts) || pts == ffmpeg.AV_NOPTS_VALUE)
                return TimeSpan.MinValue;

            return TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * 1000 * pts / timeBase, 0));
        }

        /// <summary>
        /// Gets a timespan given a timestamp and a timebase.
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns>The TimeSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ToTimeSpan(this long pts, double timeBase)
        {
            return ((double)pts).ToTimeSpan(timeBase);
        }

        /// <summary>
        /// Gets a timespan given a timestamp (in AV_TIME_BASE units)
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <returns>The TimeSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ToTimeSpan(this double pts)
        {
            return ToTimeSpan(pts, ffmpeg.AV_TIME_BASE);
        }

        /// <summary>
        /// Gets a timespan given a timestamp (in AV_TIME_BASE units)
        /// </summary>
        /// <param name="pts">The PTS.</param>
        /// <returns>The TimeSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ToTimeSpan(this long pts)
        {
            return ((double)pts).ToTimeSpan();
        }

        /// <summary>
        /// Converts a fraction to a double
        /// </summary>
        /// <param name="rational">The rational.</param>
        /// <returns>The value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ToDouble(this AVRational rational)
        {
            return (double)rational.num / rational.den;
        }

        #endregion
    }
}
