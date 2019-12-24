namespace Unosquare.FFME
{
    using Common;
    using Container;
    using Diagnostics;
    using FFmpeg.AutoGen;
    using Primitives;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public static partial class Utilities
    {
        /// <summary>
        /// Reads all the blocks of the specified media type from the source url.
        /// </summary>
        /// <param name="mediaSource">The subtitles URL.</param>
        /// <param name="sourceType">Type of the source.</param>
        /// <param name="parent">The parent.</param>
        /// <returns>A buffer containing all the blocks.</returns>
        internal static MediaBlockBuffer LoadBlocks(string mediaSource, MediaType sourceType, ILoggingHandler parent)
        {
            if (string.IsNullOrWhiteSpace(mediaSource))
                throw new ArgumentNullException(nameof(mediaSource));

            using (var tempContainer = new MediaContainer(mediaSource, null, parent))
            {
                tempContainer.Initialize();

                // Skip reading and decoding unused blocks
                tempContainer.MediaOptions.IsAudioDisabled = sourceType != MediaType.Audio;
                tempContainer.MediaOptions.IsVideoDisabled = sourceType != MediaType.Video;
                tempContainer.MediaOptions.IsSubtitleDisabled = sourceType != MediaType.Subtitle;
                tempContainer.MediaOptions.IsDataDisabled = sourceType != MediaType.Data;

                // Open the container
                tempContainer.Open();
                if (tempContainer.Components.Main == null || tempContainer.Components.MainMediaType != sourceType)
                    throw new MediaContainerException($"Could not find a stream of type '{sourceType}' to load blocks from");

                // read all the packets and decode them
                var outputFrames = new List<MediaFrame>(1024 * 8);
                while (true)
                {
                    tempContainer.Read();
                    var frames = tempContainer.Decode();
                    foreach (var frame in frames)
                    {
                        if (frame.MediaType != sourceType)
                            continue;

                        outputFrames.Add(frame);
                    }

                    if (frames.Count <= 0 && tempContainer.IsAtEndOfStream)
                        break;
                }

                // Build the result
                var result = new MediaBlockBuffer(outputFrames.Count, sourceType);
                foreach (var frame in outputFrames)
                {
                    result.Add(frame, tempContainer);
                }

                tempContainer.Close();
                return result;
            }
        }

        /// <summary>
        /// Gets the <see cref="MediaBlockBuffer"/> for the main media type of the specified media container.
        /// </summary>
        /// <param name="blocks">The blocks.</param>
        /// <param name="container">The container.</param>
        /// <returns>The block buffer of the main media type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static MediaBlockBuffer Main(this MediaTypeDictionary<MediaBlockBuffer> blocks, MediaContainer container) =>
            blocks[container.Components?.MainMediaType ?? MediaType.None];

        /// <summary>
        /// Excludes the type of the media.
        /// </summary>
        /// <param name="all">All.</param>
        /// <param name="main">The main.</param>
        /// <returns>An array without the media type.</returns>
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
        /// <param name="streamStartTime">The Stream Start time.</param>
        /// <param name="pictureStartTime">The picture Start Time.</param>
        /// <param name="frameRate">The stream's average framerate (not time base).</param>
        /// <returns>
        /// The serial picture number.
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
        /// <param name="pictureNumber">The picture number.</param>
        /// <param name="frameRate">The frame rate.</param>
        /// <returns>The FFmpeg computed SMTPE Time code.</returns>
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

        /// <summary>
        /// Clamps the specified value between the minimum and the maximum.
        /// </summary>
        /// <typeparam name="T">The type of value to clamp.</typeparam>
        /// <param name="value">The value.</param>
        /// <param name="min">The minimum.</param>
        /// <param name="max">The maximum.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T Clamp<T>(this T value, T min, T max)
            where T : struct, IComparable
        {
            if (value.CompareTo(min) < 0) return min;
            return value.CompareTo(max) > 0 ? max : value;
        }

        /// <summary>
        /// Finds the index of the item that is on or greater than the specified search value.
        /// </summary>
        /// <typeparam name="TItem">The generic collection type.</typeparam>
        /// <typeparam name="TComparable">The value type to compare to.</typeparam>
        /// <param name="items">The items.</param>
        /// <param name="value">The value.</param>
        /// <returns>The find index. Returns -1 if not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int StartIndexOf<TItem, TComparable>(this IList<TItem> items, TComparable value)
            where TItem : IComparable<TComparable>
        {
            var itemCount = items.Count;

            // fast condition checking
            if (itemCount <= 0) return -1;
            if (itemCount == 1) return 0;

            // variable setup
            var lowIndex = 0;
            var highIndex = itemCount - 1;

            // edge condition checking
            if (items[lowIndex].CompareTo(value) >= 0) return -1;
            if (items[highIndex].CompareTo(value) <= 0) return highIndex;

            // binary search
            while (highIndex - lowIndex > 1)
            {
                var midIndex = lowIndex + ((highIndex - lowIndex) / 2);
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
        /// Converts a hexadecimal string to a byte array.
        /// </summary>
        /// <param name="hexString">The hexadecimal string to convert.</param>
        /// <returns>The byte array with the data of the hexadecimal string.</returns>
        internal static byte[] HexToBytes(this string hexString)
        {
            if (hexString.Length % 2 != 0)
                throw new ArgumentException($"The binary key cannot have an odd number of digits: {hexString}");

            var data = new byte[hexString.Length / 2];
            for (int index = 0; index < data.Length; index++)
            {
                var byteValue = hexString.Substring(index * 2, 2);
                data[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return data;
        }
    }
}
