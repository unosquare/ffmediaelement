namespace Unosquare.FFME.Shared
{
    using Decoding;
    using FFmpeg.AutoGen;
    using Primitives;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides various helpers and extension methods.
    /// </summary>
    public static class Extensions
    {
        #region Audio Processing Extensions

        /// <summary>
        /// Puts a short value in the target buffer as bytes
        /// </summary>
        /// <param name="buffer">The target.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PutAudioSample(this byte[] buffer, int offset, short value)
        {
            if (BitConverter.IsLittleEndian)
            {
                buffer[offset] = (byte)(value & 0x00ff); // set the LSB
                buffer[offset + 1] = (byte)(value >> 8); // set the MSB
                return;
            }

            buffer[offset] = (byte)(value >> 8); // set the MSB
            buffer[offset + 1] = (byte)(value & 0x00ff); // set the LSB
        }

        /// <summary>
        /// Gets the a signed 16 bit integer at the given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The signed integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short GetAudioSample(this byte[] buffer, int offset) =>
            BitConverter.ToInt16(buffer, offset);

        /// <summary>
        /// Gets the audio sample amplitude (absolute value of the sample).
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The sample amplitude</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short GetAudioSampleAmplitude(this byte[] buffer, int offset)
        {
            var value = buffer.GetAudioSample(offset);
            return value == short.MinValue ? short.MaxValue : Math.Abs(value);
        }

        /// <summary>
        /// Gets the audio sample level for 0 to 1.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The amplitude level</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetAudioSampleLevel(this byte[] buffer, int offset)
        {
            return buffer.GetAudioSampleAmplitude(offset) / Convert.ToDouble(short.MaxValue);
        }

        #endregion

        #region Output Debugging

        /// <summary>
        /// Returns a formatted timestamp string in Seconds
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <returns>The formatted string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format(this TimeSpan ts)
        {
            return ts == TimeSpan.MinValue ?
                $"{"N/A",10}" :
                $"{ts.TotalSeconds,10:0.000}";
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
        /// Returns a formatted string, dividing by the specified
        /// factor. Useful for debugging longs with byte positions or sizes.
        /// </summary>
        /// <param name="ts">The timestamp.</param>
        /// <param name="divideBy">The divide by.</param>
        /// <returns>The formatted string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format(this long ts, double divideBy) =>
            Math.Abs(divideBy - 1d) <= double.Epsilon ? $"{ts,10:#,##0}" : $"{ts / divideBy,10:#,##0.000}";

        /// <summary>
        /// Returns a formatted string.
        /// Useful for debugging longs with byte positions or sizes.
        /// </summary>
        /// <param name="ts">The timestamp.</param>
        /// <returns>The formatted string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format(this long ts) => Format(ts, 1);

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
            var factor = Convert.ToInt32(value / multiple);
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
            if (double.IsNaN(pts) || Math.Abs(pts - ffmpeg.AV_NOPTS_VALUE) <= double.Epsilon)
                return TimeSpan.MinValue;

            return TimeSpan.FromTicks(timeBase.den == 0 ?
                Convert.ToInt64(TimeSpan.TicksPerMillisecond * 1000 * pts / ffmpeg.AV_TIME_BASE) :
                Convert.ToInt64(TimeSpan.TicksPerMillisecond * 1000 * pts * timeBase.num / timeBase.den));
        }

        /// <summary>
        /// Converts a timespan to an AV_TIME_BASE compatible timestamp
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="timeBase">The time base.</param>
        /// <returns>
        /// A long, ffmpeg compatible timestamp
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ToLong(this TimeSpan ts, AVRational timeBase)
        {
            return Convert.ToInt64(ts.TotalSeconds * timeBase.den / timeBase.num); // (secs) * (units) / (secs) = (units)
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
            return Convert.ToDouble(pts).ToTimeSpan(timeBase);
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
            if (double.IsNaN(pts) || Math.Abs(pts - ffmpeg.AV_NOPTS_VALUE) <= double.Epsilon)
                return TimeSpan.MinValue;

            return TimeSpan.FromTicks(Convert.ToInt64(TimeSpan.TicksPerMillisecond * 1000 * pts / timeBase));
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
            return Convert.ToDouble(pts).ToTimeSpan(timeBase);
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
            return Convert.ToDouble(pts).ToTimeSpan();
        }

        /// <summary>
        /// Converts a fraction to a double
        /// </summary>
        /// <param name="rational">The rational.</param>
        /// <returns>The value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ToDouble(this AVRational rational)
        {
            if (rational.den == 0) return 0; // prevent overflows.
            return Convert.ToDouble(rational.num) / Convert.ToDouble(rational.den);
        }

        /// <summary>
        /// Normalizes precision of the TimeSpan to the nearest whole millisecond.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>The normalized, whole-millisecond timespan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan Normalize(this TimeSpan source)
        {
            return TimeSpan.FromSeconds(source.TotalSeconds);
        }

        /// <summary>
        /// Clamps the specified value between the minimum and the maximum
        /// </summary>
        /// <typeparam name="T">The type of value to clamp</typeparam>
        /// <param name="value">The value.</param>
        /// <param name="min">The minimum.</param>
        /// <param name="max">The maximum.</param>
        /// <returns>A value that indicates the relative order of the objects being compared</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Clamp<T>(this T value, T min, T max)
            where T : struct, IComparable
        {
            if (value.CompareTo(min) < 0) return min;
            return value.CompareTo(max) > 0 ? max : value;
        }

        #endregion

        #region Other Methods

        /// <summary>
        /// Finds the index of the item that is on or greater than the specified search value
        /// </summary>
        /// <typeparam name="T">The generic collection type</typeparam>
        /// <typeparam name="V">The value type to compare to</typeparam>
        /// <param name="items">The items.</param>
        /// <param name="value">The value.</param>
        /// <returns>The find index. Returns -1 if not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int StartIndexOf<T, V>(this IList<T> items, V value)
            where T : IComparable<V>
        {
            var itemCount = items.Count;

            // fast condition checking
            if (itemCount <= 0) return -1;
            if (itemCount == 1) return 0;

            // variable setup
            var lowIndex = 0;
            var highIndex = itemCount - 1;
            var midIndex = 0;

            // edge condition checking
            if (items[lowIndex].CompareTo(value) >= 0) return -1;
            if (items[highIndex].CompareTo(value) <= 0) return highIndex;

            // binary search
            while (highIndex - lowIndex > 1)
            {
                midIndex = lowIndex + ((highIndex - lowIndex) / 2);
                if (items[midIndex].CompareTo(value) > 0)
                    highIndex = midIndex;
                else
                    lowIndex = midIndex;
            }

            // linear search
            for (var i = highIndex; i >= lowIndex; i--)
            {
                if (items[i].CompareTo(value) <= 0)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Gets the <see cref="MediaBlockBuffer"/> for the main media type of the specified media container.
        /// </summary>
        /// <param name="blocks">The blocks.</param>
        /// <param name="container">The container.</param>
        /// <returns>The block buffer of the main media type</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static MediaBlockBuffer Main(this MediaTypeDictionary<MediaBlockBuffer> blocks, MediaContainer container) =>
            blocks[container.Components?.MainMediaType ?? MediaType.None];

        /// <summary>
        /// Excludes the type of the media.
        /// </summary>
        /// <param name="all">All.</param>
        /// <param name="main">The main.</param>
        /// <returns>An array without the media type</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static MediaType[] Except(this IEnumerable<MediaType> all, MediaType main)
        {
            var result = new List<MediaType>(4);
            foreach (var item in all)
            {
                if (item != main)
                    result.Add(item);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Computes the picture number.
        /// </summary>
        /// <param name="streamStartTime">The Stream Start time</param>
        /// <param name="pictureStartTime">The picture Start Time</param>
        /// <param name="frameRate">The stream's average framerate (not time base)</param>
        /// <returns>
        /// The serial picture number
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ComputePictureNumber(TimeSpan streamStartTime, TimeSpan pictureStartTime, AVRational frameRate)
        {
            var streamTicks = streamStartTime == TimeSpan.MinValue ? 0 : streamStartTime.Ticks;
            var frameTicks = pictureStartTime == TimeSpan.MinValue ? 0 : pictureStartTime.Ticks;

            if (frameTicks < streamTicks)
                frameTicks = streamTicks;

            return 1L + (long)Math.Round(TimeSpan.FromTicks(frameTicks - streamTicks).TotalSeconds * frameRate.num / frameRate.den, 0, MidpointRounding.ToEven);
        }

        /// <summary>
        /// Computes the smtpe time code.
        /// </summary>
        /// <param name="pictureNumber">The picture number</param>
        /// <param name="frameRate">The frame rate.</param>
        /// <returns>The FFmpeg computed SMTPE Time code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe string ComputeSmtpeTimeCode(long pictureNumber, AVRational frameRate)
        {
            var pictureIndex = pictureNumber - 1;
            var frameIndex = Convert.ToInt32(pictureIndex >= int.MaxValue ? pictureIndex % int.MaxValue : pictureIndex);
            var timeCodeInfo = (AVTimecode*)ffmpeg.av_malloc((ulong)Marshal.SizeOf(typeof(AVTimecode)));
            ffmpeg.av_timecode_init(timeCodeInfo, frameRate, 0, 0, null);
            var isNtsc = frameRate.num == 30000 && frameRate.den == 1001;
            var adjustedFrameNumber = isNtsc ?
                ffmpeg.av_timecode_adjust_ntsc_framenum2(frameIndex, Convert.ToInt32(timeCodeInfo->fps)) :
                frameIndex;

            var timeCode = ffmpeg.av_timecode_get_smpte_from_framenum(timeCodeInfo, adjustedFrameNumber);
            var timeCodeBuffer = (byte*)ffmpeg.av_malloc(ffmpeg.AV_TIMECODE_STR_SIZE);

            ffmpeg.av_timecode_make_smpte_tc_string(timeCodeBuffer, timeCode, 1);
            var result = Marshal.PtrToStringAnsi((IntPtr)timeCodeBuffer);

            ffmpeg.av_free(timeCodeInfo);
            ffmpeg.av_free(timeCodeBuffer);

            return result;
        }

        #endregion
    }
}
