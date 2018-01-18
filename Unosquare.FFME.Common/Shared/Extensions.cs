namespace Unosquare.FFME.Shared
{
    using FFmpeg.AutoGen;
    using Primitives;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;

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
                return $"{"N/A", 10}";
            else
                return $"{ts.TotalSeconds, 10:0.000}";
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
            return $"{DateTime.UtcNow.Subtract(dt).TotalMilliseconds, 6:0}";
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
            return divideBy == 1 ? $"{ts, 10:#,##0}" : $"{ts / divideBy, 10:#,##0.000}";
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

        #region Faster-than-Linq replacements

        /// <summary>
        /// Determines whether the event is in its set state.
        /// </summary>
        /// <param name="m">The event.</param>
        /// <returns>
        ///   <c>true</c> if the specified m is set; otherwise, <c>false</c>.
        /// </returns>
        internal static bool IsSet(this ManualResetEvent m)
        {
            return m?.WaitOne(0) ?? true;
        }

        /// <summary>
        /// Gets the fundamental (audio or video only) auxiliary media types.
        /// </summary>
        /// <param name="all">All.</param>
        /// <param name="main">The main.</param>
        /// <returns>The non-main audio or video media types</returns>
        internal static MediaType[] FundamentalAuxsFor(this MediaType[] all, MediaType main)
        {
            var result = new List<MediaType>(16);
            var current = MediaType.None;
            for (var i = 0; i < all.Length; i++)
            {
                current = all[i];
                if (current != main && (current == MediaType.Audio || current == MediaType.Video))
                    result.Add(current);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Excludes the type of the media.
        /// </summary>
        /// <param name="all">All.</param>
        /// <param name="main">The main.</param>
        /// <returns>An array without the media type</returns>
        internal static MediaType[] ExcludeMediaType(this MediaType[] all, MediaType main)
        {
            var result = new List<MediaType>(16);
            var current = MediaType.None;
            for (var i = 0; i < all.Length; i++)
            {
                current = all[i];
                if (current != main)
                    result.Add(current);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Joins the media types.
        /// </summary>
        /// <param name="main">The main.</param>
        /// <param name="with">The with.</param>
        /// <returns>An array of the media types</returns>
        internal static MediaType[] JoinMediaTypes(this MediaType main, MediaType[] with)
        {
            var result = new List<MediaType>(16) { main };
            result.AddRange(with);
            return result.ToArray();
        }

        /// <summary>
        /// Determines whether the array contains the media type
        /// </summary>
        /// <param name="all">All.</param>
        /// <param name="t">The t.</param>
        /// <returns>True if it exists in the array</returns>
        internal static bool HasMediaType(this MediaType[] all, MediaType t)
        {
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] == t) return true;
            }

            return false;
        }

        /// <summary>
        /// Deep-copies the array
        /// </summary>
        /// <param name="all">All.</param>
        /// <returns>The copy of the array</returns>
        internal static MediaType[] DeepCopy(this MediaType[] all)
        {
            var result = new List<MediaType>(16);
            for (var i = 0; i < all.Length; i++)
            {
                result.Add(all[i]);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Verifies all fundamental (audio and video) components are greater than zero
        /// </summary>
        /// <param name="all">All.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// True if all components are greater than the value
        /// </returns>
        internal static bool FundamentalsGreaterThan(this MediaTypeDictionary<int> all, int value)
        {
            var hasFundamentals = false;
            foreach (var kvp in all)
            {
                // Skip over non-fundamental types
                if (kvp.Key != MediaType.Audio && kvp.Key != MediaType.Video)
                    continue;

                hasFundamentals = true;
                if (kvp.Value <= value) return false;
            }

            return hasFundamentals;
        }

        /// <summary>
        /// Gets the sum of all the values in the keyed dictionary.
        /// </summary>
        /// <param name="all">All.</param>
        /// <returns>The sum of all values.</returns>
        internal static int GetSum(this MediaTypeDictionary<int> all)
        {
            var result = default(int);
            foreach (var kvp in all)
                result += kvp.Value;

            return result;
        }

        #endregion
    }
}
