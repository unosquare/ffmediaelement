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
        /// Gets the a signed 16 bit integer at the guven offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The signed integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short GetAudioSample(this byte[] buffer, int offset)
        {
            // return (short)(buffer[offset] | (buffer[offset + 1] << 8));
            return BitConverter.ToInt16(buffer, offset);
        }

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
            if (value == short.MinValue)
                return short.MaxValue;

            return Math.Abs(value);
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
        /// <param name="ts">The timestamp.</param>
        /// <param name="divideBy">The divide by.</param>
        /// <returns>The formatted string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Format(this long ts, double divideBy)
        {
            return divideBy == 1 ? $"{ts,10:#,##0}" : $"{ts / divideBy,10:#,##0.000}";
        }

        /// <summary>
        /// Returns a fromatted string.
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
            if (double.IsNaN(pts) || pts == ffmpeg.AV_NOPTS_VALUE)
                return TimeSpan.MinValue;

            if (timeBase.den == 0)
                return TimeSpan.FromTicks(Convert.ToInt64(TimeSpan.TicksPerMillisecond * 1000 * pts / ffmpeg.AV_TIME_BASE));

            return TimeSpan.FromTicks(Convert.ToInt64(TimeSpan.TicksPerMillisecond * 1000 * pts * timeBase.num / timeBase.den));
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
            if (double.IsNaN(pts) || pts == ffmpeg.AV_NOPTS_VALUE)
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
        /// <returns>The normalized, whole-milliscond timespan</returns>
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

        #region Faster-than-Linq replacements

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
        /// <param name="startTime">The start time.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="startNumber">The start number.</param>
        /// <returns>
        /// The serial picture number
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ComputePictureNumber(TimeSpan startTime, TimeSpan duration, long startNumber) =>
            startNumber + Convert.ToInt64(Convert.ToDouble(startTime.Ticks) / duration.Ticks);

        /// <summary>
        /// Computes the smtpe time code.
        /// </summary>
        /// <param name="streamStartTime">The start time offset.</param>
        /// <param name="frameDuration">The duration.</param>
        /// <param name="frameTimeBase">The time base.</param>
        /// <param name="frameNumber">The display picture number.</param>
        /// <returns>The FFmpeg computed SMTPE Timecode</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe string ComputeSmtpeTimeCode(TimeSpan streamStartTime, TimeSpan frameDuration, AVRational frameTimeBase, long frameNumber)
        {
            // Drop the days in the stream start time
            if (streamStartTime.Days > 0)
                streamStartTime = streamStartTime.Subtract(TimeSpan.FromDays(streamStartTime.Days));

            // Adjust to int value
            if (frameNumber > int.MaxValue)
                frameNumber = frameNumber % int.MaxValue;

            frameNumber--; // tun picture number into picture index.
            var timeCodeInfo = (AVTimecode*)ffmpeg.av_malloc((ulong)Marshal.SizeOf(typeof(AVTimecode)));
            var startFrameNumber = ComputePictureNumber(streamStartTime, frameDuration, 0);

            // Adjust to int value
            if (startFrameNumber > int.MaxValue)
                startFrameNumber = startFrameNumber % int.MaxValue;

            ffmpeg.av_timecode_init(timeCodeInfo, frameTimeBase, 0, Convert.ToInt32(startFrameNumber), null);
            var isNtsc = frameTimeBase.num == 30000 && frameTimeBase.den == 1001;
            var adjustedFrameNumber = isNtsc ?
                ffmpeg.av_timecode_adjust_ntsc_framenum2(Convert.ToInt32(frameNumber), Convert.ToInt32(timeCodeInfo->fps)) :
                Convert.ToInt32(frameNumber);

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
